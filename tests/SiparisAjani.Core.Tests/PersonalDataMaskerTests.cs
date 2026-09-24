using SiparisAjani.Core.Models;
using SiparisAjani.Core.Text;

namespace SiparisAjani.Core.Tests;

public class PersonalDataMaskerTests
{
    private readonly PersonalDataMasker _masker = new();

    [Theory]
    [InlineData("Tel: 0500 000 00 01", "phone", "[telefon]")]
    [InlineData("Beni +90 500 000 00 06 dan ara", "phone", "[telefon]")]
    [InlineData("numaram 0500-000-00-07", "phone", "[telefon]")]
    [InlineData("e-posta: satinalma@example.com", "email", "[e-posta]")]
    [InlineData("IBAN: TR00 0001 2345 6789 0123 4567 89", "iban", "[iban]")]
    [InlineData("TR000006200000000123456789 hesabından", "iban", "[iban]")]
    [InlineData("TC: 12345678901", "nationalId", "[kimlik]")]
    [InlineData("Merhaba Hasan abi", "name", "[ad]")]
    [InlineData("Merhaba Ahmet Bey,", "name", "[ad]")]
    [InlineData("Ayşe Hanım selam", "name", "[ad]")]
    public void Mask_KisiselVeriyiYerTutucuylaDegistirir(string text, string expectedField, string placeholder)
    {
        MaskingResult result = _masker.Mask(text);

        Assert.Contains(expectedField, result.MaskedFields);
        Assert.Contains(placeholder, result.MaskedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Mask_DegerinKendisiniAslaDondurmez()
    {
        MaskingResult result = _masker.Mask("Tel: 0500 000 00 01, e-posta ornek@example.com, TC 12345678901");

        Assert.DoesNotContain("0500", result.MaskedText, StringComparison.Ordinal);
        Assert.DoesNotContain("ornek@example.com", result.MaskedText, StringComparison.Ordinal);
        Assert.DoesNotContain("12345678901", result.MaskedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Mask_IbanTelefondanOnceIslenir()
    {
        MaskingResult result = _masker.Mask("Ödeme: TR00 0001 2345 6789 0123 4567 89");

        Assert.Contains("iban", result.MaskedFields);
        Assert.DoesNotContain("phone", result.MaskedFields);
    }

    [Theory]
    [InlineData("Günaydın usta")]
    [InlineData("Selam abi kolay gelsin")]
    [InlineData("ustam merhaba")]
    public void Mask_SelamlasmaKelimesiniAdSanmaz(string text)
    {
        MaskingResult result = _masker.Mask(text);

        Assert.DoesNotContain("name", result.MaskedFields);
    }

    [Fact]
    public void Mask_OnUcHaneliBarkoduKimlikSanmaz()
    {
        MaskingResult result = _masker.Mask("8694020351963;12;adet");

        Assert.DoesNotContain("nationalId", result.MaskedFields);
        Assert.Contains("8694020351963", result.MaskedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Mask_UrunEbatlariniBozmaz()
    {
        MaskingResult result = _masker.Mask("5 kutu 4x40 sunta vidası, 3.5x25 alçıpan vidası");

        Assert.Empty(result.MaskedFields);
        Assert.Contains("4x40", result.MaskedText, StringComparison.Ordinal);
        Assert.Contains("3.5x25", result.MaskedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Mask_BosMetinIcinBosSonucDoner()
    {
        MaskingResult result = _masker.Mask("   ");

        Assert.Empty(result.MaskedFields);
    }

    [Fact]
    public void Mask_OrnekSiparislerdeBeklenenTurleriBulur()
    {
        foreach (SampleOrder sample in TestData.Samples)
        {
            MaskingResult result = _masker.Mask(sample.Text);
            foreach (string expected in sample.MaskedFields)
            {
                Assert.True(
                    result.MaskedFields.Contains(expected),
                    $"{sample.Id}: '{expected}' maskelenmedi (gelen: {string.Join(",", result.MaskedFields)})");
            }
        }
    }
}