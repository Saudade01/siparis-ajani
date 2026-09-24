using SiparisAjani.Core.Models;

namespace SiparisAjani.Core.Abstractions;

/// <summary>
/// Metindeki kişisel verileri (telefon, e-posta, IBAN, kimlik numarası, hitap) yer tutucuyla değiştirir.
/// Metin modele gönderilmeden ve günlüğe yazılmadan önce mutlaka buradan geçer.
/// </summary>
public interface IPersonalDataMasker
{
    MaskingResult Mask(string text);
}