// بيانات المنتج اللي البرنامج بيبعتها وقت المزامنة (الاسم/السعر/الكمية - مصدرها برنامج المحل فقط)
public record SyncProductDto(string Barcode, string ProductName, decimal SalePrice, int Quantity);

// بيانات المنتج زي ما بتتعرض في الكتالوج العام - فيها حقول إضافية (صورة/سعر قبل الخصم)
// بتتضاف من صفحة إدارة الموقع نفسها، مش من برنامج المحل
public record CatalogProductDto(string Barcode, string ProductName, decimal SalePrice, int Quantity, bool HasImage, decimal? OriginalPrice);
