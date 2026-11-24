using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using MDGsm.Client;
using MDGsm.IdentityAccess.Contracts;
using MDGsm.Persons.Contracts;

namespace MDGsm.Desktop;

public partial class MainWindow
{
    private HttpClient _apiHttpClient = null!;
    private IdentityAccessApiClient _identityAccessClient = null!;
    private PersonsApiClient _personsApiClient = null!;
    private readonly Guid _clientInstanceId = Guid.CreateVersion7();
    private string? _apiConfigurationError;
    private string? _accessToken;
    private string? _signedInUsername;
    private bool _mustChangePassword;
    private PendingQuickRegistration? _pendingQuickRegistration;

    public ObservableCollection<PartyQuickRegistrationCandidateContract> PersonsCandidates { get; } = [];

    private static readonly JsonSerializerOptions PersonsJsonOptions = new(JsonSerializerDefaults.Web);

    private void InitializePersonsWorkspace()
    {
        _apiHttpClient = new HttpClient();
        var baseUrl = "http://127.0.0.1:5080/";
        var timeoutSeconds = 5;
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        try
        {
            if (File.Exists(settingsPath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (document.RootElement.TryGetProperty("Api", out var api) && api.ValueKind == JsonValueKind.Object)
                {
                    if (api.TryGetProperty("BaseUrl", out var urlElement) &&
                        urlElement.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(urlElement.GetString()))
                        baseUrl = urlElement.GetString()!;
                    if (api.TryGetProperty("TimeoutSeconds", out var timeoutElement) &&
                        timeoutElement.TryGetInt32(out var configuredTimeout))
                        timeoutSeconds = configuredTimeout;
                }
            }

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                 !(string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback)) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
                _apiConfigurationError = "نشانی API نامعتبر است. برای سیستم‌های راه‌دور HTTPS لازم است؛ HTTP فقط برای اتصال Loopback مجاز است.";
            else if (timeoutSeconds is < 1 or > 60)
                _apiConfigurationError = "TimeoutSeconds در appsettings.json باید بین ۱ تا ۶۰ باشد.";
            else
            {
                _apiHttpClient.BaseAddress = new Uri(uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
                    ? uri.AbsoluteUri
                    : uri.AbsoluteUri + "/", UriKind.Absolute);
                _apiHttpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            }
        }
        catch (JsonException)
        {
            _apiConfigurationError = "فایل appsettings.json قابل خواندن نیست؛ تنظیمات اتصال API را اصلاح کنید.";
        }
        catch (IOException)
        {
            _apiConfigurationError = "فایل تنظیمات API خوانده نشد.";
        }

        _identityAccessClient = new IdentityAccessApiClient(_apiHttpClient);
        _personsApiClient = new PersonsApiClient(_apiHttpClient);
    }

    private async Task ClosePersonsWorkspaceAsync()
    {
        var token = _accessToken;
        _accessToken = null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            try { await _identityAccessClient.LogoutAsync(token); }
            catch (Exception ex) when (ex is ApiClientException or HttpRequestException or TaskCanceledException) { }
        }
        _apiHttpClient?.Dispose();
    }

    private void ShowPersonsWorkspace()
    {
        _menuCloseTimer.Stop();
        CloseActiveMenu();
        DashboardView.Visibility = Visibility.Collapsed;
        PersonsPreviewView.Visibility = Visibility.Visible;
        PersonsResultPanel.Visibility = Visibility.Collapsed;
        PersonsCandidatesPanel.Visibility = Visibility.Collapsed;
        PersonsRetryPanel.Visibility = Visibility.Collapsed;
        PersonsMessageText.Text = string.Empty;
        UpdatePersonsAuthenticationView();
        StatusText.Text = "ماژول اشخاص";
        DetailsText.Text = "ارتباط فقط از طریق نشست معتبر IdentityAccess و API نسخه‌دار انجام می‌شود.";
    }

    private void BackToDashboardButton_Click(object sender, RoutedEventArgs e)
    {
        PersonsPreviewView.Visibility = Visibility.Collapsed;
        DashboardView.Visibility = Visibility.Visible;
        StatusText.Text = "داشبورد آماده است.";
        DetailsText.Text = "داده‌های نمایشی داشبورد مستقل از ماژول اشخاص هستند.";
    }

    private void UpdatePersonsAuthenticationView()
    {
        var signedIn = !string.IsNullOrWhiteSpace(_accessToken) && !_mustChangePassword;
        PersonsLoginPanel.Visibility = _accessToken is null && !_mustChangePassword
            ? Visibility.Visible
            : Visibility.Collapsed;
        PersonsPasswordChangePanel.Visibility = _mustChangePassword
            ? Visibility.Visible
            : Visibility.Collapsed;
        PersonsWorkspacePanel.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
        PersonsSessionBadgeText.Text = signedIn
            ? "نشست معتبر"
            : _mustChangePassword ? "تغییر گذرواژه لازم است" : "ورود انجام نشده";
        PersonsSignedInAsText.Text = signedIn
            ? $"کاربر واردشده: {_signedInUsername} · نشست فقط در حافظه این اجرا"
            : string.Empty;

        if (_apiConfigurationError is not null && !signedIn && !_mustChangePassword)
            PersonsLoginMessageText.Text = _apiConfigurationError;
    }

    private async void PersonsLoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_apiConfigurationError is not null)
        {
            PersonsLoginMessageText.Text = _apiConfigurationError;
            return;
        }

        var username = PersonsUsernameBox.Text.Trim();
        var password = PersonsLoginPasswordBox.Password;
        if (username.Length == 0 || password.Length == 0)
        {
            PersonsLoginMessageText.Text = "نام کاربری و گذرواژه را وارد کنید.";
            return;
        }

        PersonsLoginButton.IsEnabled = false;
        PersonsLoginMessageText.Text = "در حال احراز هویت...";
        try
        {
            var login = await _identityAccessClient.LoginAsync(username, password, _clientInstanceId);
            _accessToken = login.AccessToken;
            _signedInUsername = username;
            _mustChangePassword = login.MustChangePassword;
            PersonsLoginPasswordBox.Clear();

            if (_mustChangePassword)
            {
                PersonsCurrentPasswordBox.Clear();
                PersonsNewPasswordBox.Clear();
                PersonsConfirmPasswordBox.Clear();
                PersonsPasswordMessageText.Text = "برای ادامه، گذرواژه اولیه را تغییر دهید.";
            }
            else
            {
                PersonsLoginMessageText.Text = string.Empty;
                PersonsMessageText.Text = "ورود موفق بود. مجوزها در هر درخواست توسط سرور بررسی می‌شوند.";
            }
            UpdatePersonsAuthenticationView();
        }
        catch (ApiClientException ex)
        {
            PersonsLoginMessageText.Text = GetIdentityAccessErrorMessage(ex);
            PersonsLoginPasswordBox.Clear();
        }
        catch (HttpRequestException)
        {
            PersonsLoginMessageText.Text = "ارتباط با API برقرار نشد. نشانی سرور و اتصال شبکه را بررسی کنید.";
            PersonsLoginPasswordBox.Clear();
        }
        catch (TaskCanceledException)
        {
            PersonsLoginMessageText.Text = "مهلت ارتباط با API تمام شد. وضعیت سرور را بررسی و دوباره تلاش کنید.";
            PersonsLoginPasswordBox.Clear();
        }
        catch (InvalidDataException)
        {
            PersonsLoginMessageText.Text = "پاسخ API معتبر نبود؛ ورود انجام نشد.";
            PersonsLoginPasswordBox.Clear();
        }
        finally
        {
            PersonsLoginButton.IsEnabled = true;
        }
    }

    private async void PersonsChangePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        var currentPassword = PersonsCurrentPasswordBox.Password;
        var newPassword = PersonsNewPasswordBox.Password;
        var confirmation = PersonsConfirmPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(_accessToken) || !_mustChangePassword)
        {
            PersonsPasswordMessageText.Text = "نشست تغییر گذرواژه معتبر نیست؛ دوباره وارد شوید.";
            return;
        }
        if (newPassword.Length is < 12 or > 128 || newPassword != confirmation ||
            string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
        {
            PersonsPasswordMessageText.Text = "گذرواژه جدید باید ۱۲ تا ۱۲۸ نویسه باشد، با تکرار آن یکسان و با گذرواژه فعلی متفاوت باشد.";
            return;
        }

        PersonsChangePasswordButton.IsEnabled = false;
        PersonsPasswordMessageText.Text = "در حال تغییر گذرواژه...";
        var passwordChanged = false;
        try
        {
            await _identityAccessClient.ChangeOwnPasswordAsync(_accessToken, currentPassword, newPassword);
            passwordChanged = true;
            _accessToken = null;
            _mustChangePassword = false;

            var login = await _identityAccessClient.LoginAsync(_signedInUsername!, newPassword, _clientInstanceId);
            _accessToken = login.AccessToken;
            _mustChangePassword = login.MustChangePassword;
            PersonsCurrentPasswordBox.Clear();
            PersonsNewPasswordBox.Clear();
            PersonsConfirmPasswordBox.Clear();

            PersonsPasswordMessageText.Text = _mustChangePassword
                ? "سرور هنوز تغییر اجباری گذرواژه را اعلام می‌کند؛ عملیات اشخاص مسدود می‌ماند."
                : string.Empty;
            if (!_mustChangePassword)
                PersonsMessageText.Text = "گذرواژه تغییر کرد و ورود مجدد موفق بود.";
            UpdatePersonsAuthenticationView();
        }
        catch (ApiClientException ex)
        {
            PersonsPasswordMessageText.Text = passwordChanged
                ? "گذرواژه تغییر کرد، اما ورود مجدد برقرار نشد. با گذرواژه جدید دوباره وارد شوید."
                : GetIdentityAccessErrorMessage(ex);
            if (passwordChanged)
            {
                _accessToken = null;
                _mustChangePassword = false;
                UpdatePersonsAuthenticationView();
            }
        }
        catch (HttpRequestException)
        {
            PersonsPasswordMessageText.Text = passwordChanged
                ? "گذرواژه تغییر کرد، اما ارتباط برای ورود مجدد قطع شد. با گذرواژه جدید دوباره وارد شوید."
                : "ارتباط با API قطع شد. نتیجه تغییر گذرواژه قابل تأیید نیست.";
            if (passwordChanged)
            {
                _accessToken = null;
                _mustChangePassword = false;
                UpdatePersonsAuthenticationView();
            }
        }
        catch (TaskCanceledException)
        {
            PersonsPasswordMessageText.Text = passwordChanged
                ? "گذرواژه تغییر کرد، اما ورود مجدد به علت پایان مهلت کامل نشد."
                : "مهلت ارتباط با API تمام شد. وضعیت تغییر گذرواژه را با مدیر سیستم بررسی کنید.";
            if (passwordChanged)
            {
                _accessToken = null;
                _mustChangePassword = false;
                UpdatePersonsAuthenticationView();
            }
        }
        finally
        {
            PersonsChangePasswordButton.IsEnabled = true;
        }
    }

    private async void PersonsLogoutButton_Click(object sender, RoutedEventArgs e)
    {
        var token = _accessToken;
        try
        {
            if (!string.IsNullOrWhiteSpace(token))
                await _identityAccessClient.LogoutAsync(token);
        }
        catch (Exception ex) when (ex is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            PersonsMessageText.Text = "خروج از سمت سرور تأیید نشد؛ توکن محلی پاک شد و نشست دیگر در این برنامه استفاده نمی‌شود.";
        }
        finally
        {
            ClearPersonsSession();
            UpdatePersonsAuthenticationView();
        }
    }

    private void ClearPersonsSession()
    {
        _accessToken = null;
        _signedInUsername = null;
        _mustChangePassword = false;
        _pendingQuickRegistration = null;
        PersonsCandidates.Clear();
        PersonsCandidatesPanel.Visibility = Visibility.Collapsed;
        PersonsResultPanel.Visibility = Visibility.Collapsed;
        PersonsRetryPanel.Visibility = Visibility.Collapsed;
        PersonsLoginPasswordBox.Clear();
        PersonsCurrentPasswordBox.Clear();
        PersonsNewPasswordBox.Clear();
        PersonsConfirmPasswordBox.Clear();
    }

    private void RegisterNaturalButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildAddress(NaturalProvinceBox.Text, NaturalCityBox.Text, NaturalPostalCodeBox.Text,
                NaturalAddressLineBox.Text, out var address, out var addressError))
        {
            PersonsMessageText.Text = addressError!;
            return;
        }

        var roles = BuildRoles(NaturalCustomerRoleCheckBox.IsChecked == true, NaturalColleagueRoleCheckBox.IsChecked == true);
        if (roles.Length == 0)
        {
            PersonsMessageText.Text = "حداقل یک نقش تجاری، یعنی مشتری یا همکار، انتخاب کنید.";
            return;
        }
        var workClassification = SelectedTag(NaturalWorkClassificationBox);
        if (!string.IsNullOrEmpty(workClassification) && !roles.Contains("Colleague", StringComparer.Ordinal))
        {
            PersonsMessageText.Text = "طبقه‌بندی شغلی فقط برای شخصی معتبر است که نقش همکار داشته باشد.";
            return;
        }

        var request = new CreateNaturalPartyRequest(
            NaturalGivenNameBox.Text.Trim(), NaturalFamilyNameBox.Text.Trim(), roles,
            NullIfBlank(NaturalNationalIdBox.Text), NullIfBlank(NaturalPrimaryMobileBox.Text),
            NullIfBlank(NaturalAlternateMobileBox.Text), NullIfBlank(NaturalLandlineBox.Text),
            NullIfBlank(NaturalEmailBox.Text), address,
            new PartyCommunicationPreferencesContract(
                NaturalRepairSmsCheckBox.IsChecked == true,
                NaturalReadySmsCheckBox.IsChecked == true,
                NaturalPromotionsSmsCheckBox.IsChecked == true),
            SelectedTag(NaturalAcquaintanceSourceBox) ?? "Unknown",
            NullIfBlank(NaturalInternalNoteBox.Text),
            string.IsNullOrEmpty(workClassification) ? null : workClassification);

        if (string.IsNullOrWhiteSpace(request.GivenName) || string.IsNullOrWhiteSpace(request.FamilyName))
        {
            PersonsMessageText.Text = "نام و نام خانوادگی لازم است.";
            return;
        }

        var quickRequest = new QuickRegisterNaturalPartyRequest(request, NaturalForceNewCheckBox.IsChecked == true);
        _ = BeginQuickRegistrationAsync(true, quickRequest, JsonSerializer.Serialize(quickRequest, PersonsJsonOptions));
    }

    private void RegisterLegalButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildAddress(LegalProvinceBox.Text, LegalCityBox.Text, LegalPostalCodeBox.Text,
                LegalAddressLineBox.Text, out var address, out var addressError))
        {
            PersonsMessageText.Text = addressError!;
            return;
        }

        var roles = BuildRoles(LegalCustomerRoleCheckBox.IsChecked == true, LegalColleagueRoleCheckBox.IsChecked == true);
        if (roles.Length == 0)
        {
            PersonsMessageText.Text = "حداقل یک نقش تجاری، یعنی مشتری یا همکار، انتخاب کنید.";
            return;
        }

        var request = new CreateLegalPartyRequest(
            LegalNameBox.Text.Trim(), NullIfBlank(LegalNationalIdBox.Text),
            NullIfBlank(LegalEconomicCodeBox.Text), NullIfBlank(LegalResponsiblePersonBox.Text), roles,
            NullIfBlank(LegalPrimaryMobileBox.Text), NullIfBlank(LegalAlternateMobileBox.Text),
            NullIfBlank(LegalLandlineBox.Text), NullIfBlank(LegalEmailBox.Text), address,
            new PartyCommunicationPreferencesContract(
                LegalRepairSmsCheckBox.IsChecked == true,
                LegalReadySmsCheckBox.IsChecked == true,
                LegalPromotionsSmsCheckBox.IsChecked == true),
            SelectedTag(LegalAcquaintanceSourceBox) ?? "Unknown",
            NullIfBlank(LegalInternalNoteBox.Text));

        if (string.IsNullOrWhiteSpace(request.LegalName))
        {
            PersonsMessageText.Text = "نام شرکت لازم است.";
            return;
        }

        var quickRequest = new QuickRegisterLegalPartyRequest(request, LegalForceNewCheckBox.IsChecked == true);
        _ = BeginQuickRegistrationAsync(false, quickRequest, JsonSerializer.Serialize(quickRequest, PersonsJsonOptions));
    }

    private async Task BeginQuickRegistrationAsync(bool isNatural, object request, string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(_accessToken) || _mustChangePassword)
        {
            PersonsMessageText.Text = "ابتدا وارد نشست معتبر شوید.";
            return;
        }

        if (_pendingQuickRegistration is null)
            _pendingQuickRegistration = new PendingQuickRegistration(isNatural, request, fingerprint, Guid.CreateVersion7());
        else if (_pendingQuickRegistration.IsNatural != isNatural ||
                 !string.Equals(_pendingQuickRegistration.PayloadFingerprint, fingerprint, StringComparison.Ordinal))
        {
            PersonsMessageText.Text = "یک درخواست با نتیجه نامشخص باز است. ابتدا همان درخواست را با شناسه قبلی تکرار کنید تا از ثبت تکراری جلوگیری شود.";
            PersonsRetryPanel.Visibility = Visibility.Visible;
            return;
        }

        await SendPendingQuickRegistrationAsync();
    }

    private async void PersonsRetryButton_Click(object sender, RoutedEventArgs e) =>
        await SendPendingQuickRegistrationAsync();

    private async void SelectPersonCandidateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.Tag is not PartyQuickRegistrationCandidateContract candidate ||
            _pendingQuickRegistration is null)
            return;

        _pendingQuickRegistration = _pendingQuickRegistration with { SelectedPartyId = candidate.PartyId };
        await SendPendingQuickRegistrationAsync();
    }

    private async Task SendPendingQuickRegistrationAsync()
    {
        var pending = _pendingQuickRegistration;
        if (pending is null || string.IsNullOrWhiteSpace(_accessToken))
        {
            PersonsMessageText.Text = "درخواست معلق یا نشست معتبر وجود ندارد.";
            return;
        }

        PersonsResultPanel.Visibility = Visibility.Visible;
        PersonsCandidatesPanel.Visibility = Visibility.Collapsed;
        PersonsRetryPanel.Visibility = Visibility.Collapsed;
        PersonsCandidates.Clear();
        PersonsResultText.Text = "در حال ارسال درخواست به سرور...";
        PersonsPartyIdText.Text = string.Empty;
        PersonsRetryButton.IsEnabled = false;
        RegisterNaturalButton.IsEnabled = false;
        RegisterLegalButton.IsEnabled = false;

        try
        {
            PartyQuickRegistrationResponse result;
            if (pending.IsNatural)
            {
                var request = ((QuickRegisterNaturalPartyRequest)pending.Request) with
                {
                    SelectedPartyId = pending.SelectedPartyId
                };
                result = await _personsApiClient.QuickRegisterNaturalAsync(_accessToken, pending.OperationId, request);
            }
            else
            {
                var request = ((QuickRegisterLegalPartyRequest)pending.Request) with
                {
                    SelectedPartyId = pending.SelectedPartyId
                };
                result = await _personsApiClient.QuickRegisterLegalAsync(_accessToken, pending.OperationId, request);
            }

            switch (result.Outcome)
            {
                case "SelectionRequired":
                    PersonsResultText.Text = "موارد مشابه پیدا شدند. یکی از آن‌ها را انتخاب کنید.";
                    PersonsCandidatesPanel.Visibility = Visibility.Visible;
                    foreach (var candidate in result.Candidates)
                        PersonsCandidates.Add(candidate);
                    break;
                case "Created":
                    PersonsResultText.Text = "شخص جدید توسط سرور ثبت شد.";
                    PersonsPartyIdText.Text = $"PartyId: {result.PartyId}";
                    CompletePendingRegistration();
                    break;
                case "ExistingPartyMatched":
                    PersonsResultText.Text = "شخص موجود شناسایی شد و نقش‌های انتخاب‌شده به همان هویت افزوده شدند.";
                    PersonsPartyIdText.Text = $"PartyId: {result.PartyId}";
                    CompletePendingRegistration();
                    break;
                case "Replayed":
                    PersonsResultText.Text = "نتیجه قبلی همین عملیات از سرور دریافت شد؛ ثبت تکراری انجام نشد.";
                    PersonsPartyIdText.Text = $"PartyId: {result.PartyId}";
                    CompletePendingRegistration();
                    break;
                default:
                    PersonsResultText.Text = "سرور نتیجه ناشناخته برگرداند. شناسه عملیات حفظ شده است.";
                    PersonsRetryPanel.Visibility = Visibility.Visible;
                    break;
            }
        }
        catch (ApiClientException ex)
        {
            PersonsResultText.Text = GetPersonsErrorMessage(ex);
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                ClearPersonsSession();
                UpdatePersonsAuthenticationView();
            }
            else
            {
                if (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
                    _pendingQuickRegistration = null;
                PersonsRetryPanel.Visibility = _pendingQuickRegistration is null ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        catch (HttpRequestException)
        {
            PersonsResultText.Text = "پاسخ سرور دریافت نشد. برای جلوگیری از ایجاد رکورد تکراری، فقط با همان شناسه عملیات تلاش مجدد کنید.";
            PersonsRetryPanel.Visibility = Visibility.Visible;
        }
        catch (TaskCanceledException)
        {
            PersonsResultText.Text = "مهلت پاسخ‌گویی سرور تمام شد. نتیجه قطعی نیست؛ فقط با همان شناسه عملیات تلاش مجدد کنید.";
            PersonsRetryPanel.Visibility = Visibility.Visible;
        }
        catch (InvalidDataException)
        {
            PersonsResultText.Text = "پاسخ سرور معتبر نبود. نتیجه قطعی نیست؛ درخواست را با همان شناسه تکرار کنید.";
            PersonsRetryPanel.Visibility = Visibility.Visible;
        }
        finally
        {
            PersonsRetryButton.IsEnabled = true;
            RegisterNaturalButton.IsEnabled = true;
            RegisterLegalButton.IsEnabled = true;
        }
    }

    private void CompletePendingRegistration()
    {
        _pendingQuickRegistration = null;
        PersonsCandidates.Clear();
        PersonsCandidatesPanel.Visibility = Visibility.Collapsed;
        PersonsRetryPanel.Visibility = Visibility.Collapsed;
        PersonsMessageText.Text = "پاسخ ثبت یا تطبیق از سرور دریافت شد.";
        NaturalForceNewCheckBox.IsChecked = false;
        LegalForceNewCheckBox.IsChecked = false;
    }

    private static string GetPersonsErrorMessage(ApiClientException exception)
    {
        if (exception.StatusCode == HttpStatusCode.ServiceUnavailable &&
            string.Equals(exception.ErrorCode, "persons_write_api_disabled", StringComparison.Ordinal))
            return "ثبت اشخاص در تنظیمات سرور فعال نشده است (HTTP 503). هیچ رکوردی ایجاد نشده؛ فعال‌سازی باید پس از عبور از گیت G1 انجام شود.";
        if (exception.StatusCode == HttpStatusCode.Forbidden)
            return "حساب فعلی مجوزهای persons.read و persons.create لازم برای ثبت سریع را ندارد.";
        if (exception.StatusCode == HttpStatusCode.Unauthorized)
            return "نشست احراز هویت منقضی یا نامعتبر شده است؛ دوباره وارد شوید.";
        if (exception.StatusCode == HttpStatusCode.Conflict)
            return "سرور درخواست را به دلیل تعارض کلید عملیات، شناسه تکراری یا نامعتبر بودن انتخاب رد کرد.";
        if (exception.StatusCode == HttpStatusCode.UnprocessableEntity)
            return "اطلاعات ورودی با قواعد ثبت اشخاص سازگار نیست. موارد را بررسی کنید.";
        return $"ثبت از سمت سرور انجام نشد (HTTP {(int)exception.StatusCode}). شناسه درخواست: {exception.RequestId ?? "در دسترس نیست"}";
    }

    private static string GetIdentityAccessErrorMessage(ApiClientException exception)
    {
        if (exception.StatusCode == HttpStatusCode.ServiceUnavailable) return "IdentityAccess روی سرور آماده یا فعال نیست (HTTP 503).";
        if (exception.StatusCode == HttpStatusCode.Unauthorized) return "نام کاربری یا گذرواژه معتبر نیست.";
        if (exception.StatusCode == HttpStatusCode.TooManyRequests) return "تعداد تلاش‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.";
        if (exception.StatusCode == HttpStatusCode.Conflict) return "ظرفیت نشست‌های هم‌زمان تکمیل شده است.";
        if (exception.StatusCode == HttpStatusCode.Forbidden) return "این نشست تا انجام تغییر گذرواژه اجازه ادامه ندارد.";
        return $"ورود انجام نشد (HTTP {(int)exception.StatusCode}). شناسه درخواست: {exception.RequestId ?? "در دسترس نیست"}";
    }

    private static string[] BuildRoles(bool customer, bool colleague)
    {
        var roles = new List<string>(2);
        if (customer) roles.Add("Customer");
        if (colleague) roles.Add("Colleague");
        return roles.ToArray();
    }

    private static string? SelectedTag(ComboBox comboBox) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryBuildAddress(
        string provinceText, string cityText, string postalCodeText, string addressLineText,
        out PartyAddressContract? address, out string? error)
    {
        address = null;
        error = null;
        var province = NullIfBlank(provinceText);
        var city = NullIfBlank(cityText);
        var postalCode = NullIfBlank(postalCodeText);
        var addressLine = NullIfBlank(addressLineText);
        var fields = new[] { province, city, postalCode, addressLine };
        if (fields.All(string.IsNullOrWhiteSpace))
            return true;
        if (fields.Any(string.IsNullOrWhiteSpace))
        {
            error = "برای ثبت نشانی، استان، شهر، کد پستی و نشانی کامل را با هم وارد کنید؛ یا همه را خالی بگذارید.";
            return false;
        }

        address = new PartyAddressContract("IR", province!, city!, postalCode!, addressLine!);
        return true;
    }

    private sealed record PendingQuickRegistration(
        bool IsNatural, object Request, string PayloadFingerprint, Guid OperationId, string? SelectedPartyId = null);
}
