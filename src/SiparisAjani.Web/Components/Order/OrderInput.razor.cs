using SiparisAjani.Contracts;

namespace SiparisAjani.Web.Components.Order;

public partial class OrderInput
{
    // Demo için gömülü sentetik örnekler. Telefon ve e-posta uydurmadır; kişi adı yoktur,
    // gerçek müşteri verisi değildir. Maskeleme bilgisinin görünmesi için bilerek iletişim bilgisi içerirler.
    private static readonly SampleOrder[] SampleOrders =
    [
        new(
            OrderSource.WhatsApp,
            """
            Merhaba abi, yarın sabah şunları hazırlar mısın:
            5 kutu 4x40 sunta vidası
            10 adet şeffaf silikon
            3 paket 8'lik dübel
            2 paket kablo bağı 200'lük
            4 adet 6'lık beton matkap ucu
            6 rulo maskeleme bandı
            1 koli kırmızı sprey boya
            Akşama kadar gelirse süper. Tel: 0532 111 22 33
            """),
        new(
            OrderSource.Email,
            """
            Konu: Haftalık sipariş

            İyi günler,
            Aşağıdaki ürünlerin bu hafta sevkini rica ederiz:

            - 20 adet teflon bant
            - 4 kutu alçıpan vidası 3,5x35
            - 2 adet poliüretan köpük 750 ml
            - 10 adet su zımparası 100 kum
            - 5 paket maket bıçağı yedeği
            - 3 adet şerit metre 5 m
            - 2 adet kaynak maskesi

            Fatura bilgileri için: satinalma@example.com
            Saygılarımızla,
            Satın alma birimi
            """)
    ];

    private sealed record SampleOrder(OrderSource Source, string Text);
}