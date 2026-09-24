namespace SiparisAjani.Core.Models;

/// <summary>
/// Maskeleme sonucu. <see cref="MaskedText"/> modele ve günlüğe gidebilir;
/// maskelenen değerlerin kendisi hiçbir zaman döndürülmez.
/// </summary>
/// <param name="MaskedText">Kişisel verileri yer tutucularla değiştirilmiş metin.</param>
/// <param name="MaskedFields">Maskelenen alan türleri (phone, email, iban, nationalId, name).</param>
public sealed record MaskingResult(string MaskedText, IReadOnlyList<string> MaskedFields);