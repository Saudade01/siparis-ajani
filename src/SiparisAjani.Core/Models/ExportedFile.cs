namespace SiparisAjani.Core.Models;

/// <summary>Dışa aktarılan dosyanın içeriği ve sunum bilgileri.</summary>
public sealed record ExportedFile(byte[] Content, string FileName, string ContentType);