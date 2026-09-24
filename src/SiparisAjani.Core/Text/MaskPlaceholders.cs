namespace SiparisAjani.Core.Text;

/// <summary>Maskeleme yer tutucuları. Ayrıştırıcılar bu metinleri "ürün değil" diye tanır.</summary>
public static class MaskPlaceholders
{
    public const string Phone = "[telefon]";
    public const string Email = "[e-posta]";
    public const string Iban = "[iban]";
    public const string NationalId = "[kimlik]";
    public const string Name = "[ad]";

    /// <summary>Maskelenen alan türlerinin sözleşmedeki adları.</summary>
    public static class Fields
    {
        public const string Phone = "phone";
        public const string Email = "email";
        public const string Iban = "iban";
        public const string NationalId = "nationalId";
        public const string Name = "name";
    }

    public static IReadOnlyList<string> All { get; } = [Phone, Email, Iban, NationalId, Name];
}