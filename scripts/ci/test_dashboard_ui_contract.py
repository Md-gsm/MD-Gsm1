#!/usr/bin/env python3
"""Source-level regression tests for the WPF dashboard layout contract.

These tests run only in development/CI; Python is not a runtime dependency of MD-Gsm.
"""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
XAML = (ROOT / "src/MDGsm.Desktop/MainWindow.xaml").read_text(encoding="utf-8")
CS = (ROOT / "src/MDGsm.Desktop/MainWindow.xaml.cs").read_text(encoding="utf-8")


class DashboardUiContractTests(unittest.TestCase):
    def test_dashboard_uses_responsive_ten_percent_insets(self):
        self.assertIn('x:Name="DashboardContentPanel"', XAML)
        self.assertIn('DashboardContentPanel.Margin = new Thickness(horizontalInset, verticalInset, horizontalInset, verticalInset);', CS)
        self.assertIn('var horizontalInset = viewportWidth * 0.10;', CS)
        self.assertIn('var verticalInset = viewportHeight * 0.10;', CS)

    def test_reception_grid_has_no_internal_vertical_scrollbar(self):
        grid_start = XAML.index('<DataGrid x:Name="ReceptionGrid"')
        grid_open_end = XAML.index('>', grid_start)
        grid_open = XAML[grid_start:grid_open_end + 1]
        self.assertIn('VerticalScrollBarVisibility="Disabled"', grid_open)
        self.assertNotIn('MaxHeight="420"', grid_open)
        self.assertIn('HorizontalScrollBarVisibility="Disabled"', grid_open)
        self.assertIn('RowHeight="49"', grid_open)
        self.assertIn('Margin="10,0,10,0"', grid_open)
        self.assertIn('UpdateReceptionGridHeight(result.Count);', CS)
        self.assertIn('ReceptionGrid.Height = columnHeaderHeight + Math.Max(0, visibleRowCount) * receptionRowHeight;', CS)

    def test_marker_is_a_dedicated_visible_rtl_edge_column(self):
        grid_start = XAML.index('<DataGrid x:Name="ReceptionGrid"')
        columns_start = XAML.index('<DataGrid.Columns>', grid_start)
        marker_start = XAML.index('<DataGridTemplateColumn Header="" Width="3.3"', columns_start)
        first_real_column = XAML.index('<DataGridTemplateColumn Header="کد پذیرش"', columns_start)
        self.assertLess(marker_start, first_real_column)
        self.assertIn('Background="{Binding MarkerBrush}"', XAML[marker_start:first_real_column])
        self.assertIn('FlowDirection="RightToLeft"', XAML[grid_start:columns_start])
        self.assertIn('HorizontalScrollBarVisibility="Disabled"', XAML[grid_start:columns_start])

    def test_delivery_color_policy_is_explicit_and_ordered(self):
        self.assertIn('CreateFrozenBrush("#E00000")', CS)  # vivid strong red, urgent
        self.assertIn('CreateFrozenBrush("#8B5CF6")', CS)  # purple, partner
        self.assertIn('CreateFrozenBrush("#10B981")', CS)  # green, tomorrow/after
        self.assertIn('CreateFrozenBrush("#F97316")', CS)  # orange, today
        marker_start = CS.index('public SolidColorBrush MarkerBrush => IsDelivered')
        marker_end = CS.index('public bool IsDelivered', marker_start)
        marker = CS[marker_start:marker_end]
        self.assertLess(marker.index('? DefaultMarkerBrush'), marker.index('? UrgentMarkerBrush'))
        self.assertLess(marker.index('? UrgentMarkerBrush'), marker.index('? PartnerMarkerBrush'))
        self.assertLess(marker.index('? PartnerMarkerBrush'), marker.index('? TodayDeliveryMarkerBrush'))
        self.assertLess(marker.index('? TodayDeliveryMarkerBrush'), marker.index('? LaterDeliveryMarkerBrush'))

    def test_today_tab_remains_the_default_filter(self):
        self.assertIn('private string _activeReceptionFilter = "deliveryToday";', CS)
        self.assertRegex(XAML, r'<Button x:Name="TabDeliveryToday"[^>]*Background="#EFF6FF"')

    def test_ui_font_sizes_are_scaled_beyond_previous_small_baseline(self):
        self.assertIn('FontSize="13.2"', XAML)  # 12 * 1.1, common label baseline
        self.assertIn('FontSize="14.3"', XAML)  # 13 * 1.1, table/text baseline
        self.assertIn('FontSize="12.1"', XAML)  # 11 * 1.1, menu baseline

    def test_product_brand_and_live_date_are_on_right_of_quick_actions(self):
        self.assertIn('x:Name="BrandActionsLayout" FlowDirection="LeftToRight"', XAML)
        self.assertRegex(XAML, r'x:Name="ProductBrandBlock" Grid.Column="2"')
        self.assertIn('x:Name="ProductBrandBlock" Grid.Column="2" Orientation="Horizontal"\n                    HorizontalAlignment="Right" VerticalAlignment="Center" FlowDirection="LeftToRight"', XAML)
        self.assertRegex(XAML, r'x:Name="QuickActionsBlock" Grid.Column="0"')
        self.assertIn('HorizontalAlignment="Left" VerticalAlignment="Center" FlowDirection="RightToLeft"', XAML[XAML.index('x:Name="QuickActionsBlock"'):XAML.index('</StackPanel>', XAML.index('x:Name="QuickActionsBlock"'))])
        self.assertIn('Text="نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"', XAML)
        self.assertIn('x:Name="LiveDateTimeText"', XAML)
        self.assertIn('نسخه پیش‌نمایش ۰٫۲٫۱۱', XAML)
        for action_id in ('QCK-01', 'QCK-02', 'QCK-03'):
            self.assertIn(f'Tag="{action_id}"', XAML)
        self.assertIn('Title="نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل | MD-Gsm v0.2.11 — پیش‌نمایش"', XAML)
        csproj = (ROOT / "src/MDGsm.Desktop/MDGsm.Desktop.csproj").read_text(encoding="utf-8")
        installer = (ROOT / "installer/MDGsm-Persons-Preview.iss").read_text(encoding="utf-8")
        build_script = (ROOT / "scripts/Build-Windows-Installer.ps1").read_text(encoding="utf-8")
        self.assertIn('<Version>0.2.11</Version>', csproj)
        self.assertIn('#pragma codepage 65001', installer)
        self.assertIn('#define AppName "نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"', installer)
        self.assertIn('OutputBaseFilename=MD-Gsm-Persons-Preview-Setup-v0.2.11', installer)
        self.assertIn('MD-Gsm-Persons-Preview-Setup-v0.2.11.exe', build_script)


    def test_dashboard_background_is_exact_requested_slate(self):
        self.assertIn('Grid Grid.Row="1" Background="#F1F5F9"', XAML)
        self.assertIn('FlowDirection="RightToLeft" Background="#F1F5F9"', XAML)

    def test_table_headers_cells_and_rows_share_alignment_contract(self):
        self.assertIn('x:Key="AlignedDataGridTextStyle"', XAML)
        self.assertIn('<Setter Property="TextAlignment" Value="Right"/>', XAML)
        self.assertIn('<Setter Property="HorizontalContentAlignment" Value="Stretch"/>', XAML)
        self.assertIn('<Setter Property="VerticalContentAlignment" Value="Center"/>', XAML)
        self.assertIn('ElementStyle="{StaticResource AlignedDataGridTextStyle}"', XAML)

    def test_persons_grid_expands_to_all_visible_rows_without_internal_scroll(self):
        grid_start = XAML.index('<DataGrid x:Name="PersonsPreviewGrid"')
        grid_open_end = XAML.index('>', grid_start)
        grid_open = XAML[grid_start:grid_open_end + 1]
        self.assertIn('HorizontalScrollBarVisibility="Disabled"', grid_open)
        self.assertIn('VerticalScrollBarVisibility="Disabled"', grid_open)
        self.assertNotIn('MaxHeight=', grid_open)
        self.assertIn('UpdatePersonsGridHeight(PreviewPersons.Count);', CS)
        self.assertIn('PersonsPreviewGrid.Height = headerHeight + Math.Max(0, visibleRowCount) * personRowHeight;', CS)

    def test_delivery_marker_is_ten_percent_thicker_than_three_px_baseline(self):
        self.assertIn('<DataGridTemplateColumn Header="" Width="3.3" MinWidth="3.3" MaxWidth="3.3"', XAML)

    def test_top_navigation_frame_is_taller_and_has_more_vertical_padding(self):
        self.assertIn('<RowDefinition Height="42"/>', XAML)
        style_start = XAML.index('<Style x:Key="TopMenuButtonStyle"')
        style_end = XAML.index('</Style>', style_start)
        style = XAML[style_start:style_end]
        self.assertIn('<Setter Property="Padding" Value="8,6"/>', style)

    def test_header_brand_is_right_quick_actions_are_left_and_marker_is_thicker(self):
        brand = XAML[XAML.index('x:Name="ProductBrandBlock"'):XAML.index('</StackPanel>', XAML.index('x:Name="ProductBrandBlock"'))]
        actions = XAML[XAML.index('x:Name="QuickActionsBlock"'):XAML.index('</StackPanel>', XAML.index('x:Name="QuickActionsBlock"'))]
        self.assertIn('Grid.Column="2"', brand)
        self.assertIn('HorizontalAlignment="Right"', brand)
        self.assertIn('Grid.Column="0"', actions)
        self.assertIn('HorizontalAlignment="Left"', actions)
        self.assertIn('Width="3.3" MinWidth="3.3" MaxWidth="3.3"', XAML)
        self.assertIn('CreateFrozenBrush("#E00000")', CS)

    def test_partner_acceptances_use_purple_code_and_subtle_purple_row(self):
        self.assertIn('<DataGridTemplateColumn Header="مشتری" Width="1.3*">', XAML)
        self.assertIn('<DataTrigger Binding="{Binding CustomerType}" Value="همکار">', XAML)
        self.assertIn('<Setter Property="Foreground" Value="#8B5CF6"/>', XAML)
        self.assertIn('<Setter Property="Background" Value="#FAF7FF"/>', XAML)
        self.assertIn('<Setter Property="BorderBrush" Value="#EDE9FE"/>', XAML)

    def test_reception_count_and_preview_disclaimer_footer_is_removed(self):
        self.assertNotIn('ReceptionRowCountText', XAML)
        self.assertNotIn('این فهرست صرفاً پیش‌نمایش رابط است', XAML)
        self.assertNotIn('نمایش ۵ مورد از ۱۲ دستگاه', XAML)
        self.assertNotIn('ReceptionRowCountText.Text', CS)

    def test_only_top_level_menu_font_is_enlarged_and_logo_stacks_above_brand_name(self):
        style_start = XAML.index('<Style x:Key="TopMenuButtonStyle"')
        style_end = XAML.index('</Style>', style_start)
        self.assertIn('<Setter Property="FontSize" Value="12.5"/>', XAML[style_start:style_end])
        cascade_start = XAML.index('<Style x:Key="CascadeMenuButtonStyle"')
        cascade_end = XAML.index('</Style>', cascade_start)
        self.assertIn('<Setter Property="FontSize" Value="12"/>', XAML[cascade_start:cascade_end])
        brand_start = XAML.index('x:Name="ProductBrandBlock"')
        brand_end = XAML.index('<!-- Main quick actions', brand_start)
        brand = XAML[brand_start:brand_end]
        self.assertIn('Width="48" Height="42"', brand)
        self.assertIn('Text="MD"', brand)
        self.assertIn('Text="MD-Gsm"', brand)
        self.assertLess(brand.index('Text="MD"'), brand.index('Text="MD-Gsm"'))


if __name__ == "__main__":
    unittest.main()
