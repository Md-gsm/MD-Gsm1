"""Policy oracle only; production .NET canonicalizer still required."""
import unicodedata, unittest
def canonical(v):
    if not isinstance(v,str) or not v: raise ValueError()
    v=unicodedata.normalize("NFC",v)
    if not 3<=len(v)<=64: raise ValueError()
    if any(unicodedata.category(c)[0]!="L" and unicodedata.category(c)!="Nd" for c in v): raise ValueError()
    scripts=set()
    for c in v:
        if unicodedata.category(c)[0]=="L":
            n=unicodedata.name(c,"")
            scripts.add("LATIN" if "LATIN" in n else "ARABIC" if "ARABIC" in n else "OTHER")
    if len(scripts)>1: raise ValueError()
    return v.casefold() if scripts=={"LATIN"} else v
class PolicyTests(unittest.TestCase):
    def test_latin_casefold(self): self.assertEqual(canonical("ali123"),canonical("ALI123"))
    def test_digits_preserved(self):
        self.assertNotEqual(canonical("کاربر۱۲۳"),canonical("کاربر١٢٣"))
        self.assertNotEqual(canonical("کاربر۱۲۳"),canonical("کاربر123"))
    def test_arabic_persian_letters_distinct(self): self.assertNotEqual(canonical("علی"),canonical("علي"))
    def test_nfc(self): self.assertEqual(canonical("éclair"),canonical("e\u0301clair"))
    def test_invalid(self):
        for v in ("ab"," ali","ali ","ali name","ali_name","ali🙂","a\u200Db","abcعلی"):
            with self.subTest(v=v), self.assertRaises(ValueError): canonical(v)
    def test_length(self):
        self.assertEqual(len(canonical("a"*64)),64)
        with self.assertRaises(ValueError): canonical("a"*65)
if __name__=="__main__": unittest.main()
