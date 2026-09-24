using SiparisAjani.Core.Text;

namespace SiparisAjani.Core.Tests;

public class TurkishTextNormalizerTests
{
    [Theory]
    [InlineData("VİDA", "vida")]
    [InlineData("IŞIK", "ışık")]
    [InlineData("Sunta Vidası", "sunta vidası")]
    public void ToLowerTr_TurkceKurallariUygular(string input, string expected)
    {
        Assert.Equal(expected, TurkishTextNormalizer.ToLowerTr(input));
    }

    [Fact]
    public void ToLowerTr_InvariantKulturdenFarklidir()
    {
        // Kültür verilmezse "I" harfi "i" olur ve "IŞIK" ile "ışık" eşleşmez.
        Assert.NotEqual("IŞIK".ToLowerInvariant(), TurkishTextNormalizer.ToLowerTr("IŞIK"));
    }

    [Theory]
    [InlineData("Sunta Vidası 4x40", "sunta vidasi 4x40")]
    [InlineData("Şeffaf Silikon", "seffaf silikon")]
    [InlineData("İç Cephe Boyası", "ic cephe boyasi")]
    [InlineData("Çakmalı Dübel", "cakmali dubel")]
    [InlineData("3,5x25", "3.5x25")]
    public void Normalize_TurkceHarfleriAsciiyeIndirger(string input, string expected)
    {
        Assert.Equal(expected, TurkishTextNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("8'lik dübel", new[] { "8", "dubel" })]
    [InlineData("10luk dubel", new[] { "10", "dubel" })]
    [InlineData("Saten Alçı 25 kg", new[] { "saten", "alci", "25" })]
    [InlineData("Boya Rulosu 25 cm", new[] { "boya", "rulosu", "25" })]
    [InlineData("köpük 750 lik", new[] { "kopuk", "750" })]
    public void Tokenize_OlcuBirimleriniAtarBoyutEkiniSadelestirir(string input, string[] expected)
    {
        Assert.Equal(expected, TurkishTextNormalizer.Tokenize(input));
    }

    [Theory]
    [InlineData("4x40", true)]
    [InlineData("3.5x25", true)]
    [InlineData("vida", false)]
    public void IsNumeric_RakamIcerenBelirteciTanir(string token, bool expected)
    {
        Assert.Equal(expected, TurkishTextNormalizer.IsNumeric(token));
    }

    [Fact]
    public void Normalize_BosGirdiIcinBosDoner()
    {
        Assert.Equal(string.Empty, TurkishTextNormalizer.Normalize(null));
        Assert.Empty(TurkishTextNormalizer.Tokenize("   "));
    }
}