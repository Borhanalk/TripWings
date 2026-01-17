# إعداد قاعدة البيانات

## المشكلة
إذا واجهت خطأ `Invalid object name 'UserWallets'` أو أي جدول آخر، فهذا يعني أن قاعدة البيانات لم يتم تحديثها بعد إضافة الجداول الجديدة.

## الحل السريع

### الطريقة 1: استخدام السكريبت (مُوصى بها)

1. افتح PowerShell في مجلد المشروع
2. قم بتشغيل السكريبت:
```powershell
.\Scripts\ResetDatabase.ps1
```

3. ثم قم بتشغيل التطبيق:
```bash
dotnet run
```

سيتم إنشاء قاعدة البيانات تلقائياً مع جميع الجداول الجديدة.

### الطريقة 2: حذف قاعدة البيانات يدوياً

1. افتح SQL Server Management Studio (SSMS) أو استخدم `sqlcmd`
2. قم بتشغيل الأمر التالي:
```sql
USE master;
GO
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'TripWingsDB')
BEGIN
    ALTER DATABASE TripWingsDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE TripWingsDB;
END
GO
```

3. ثم قم بتشغيل التطبيق:
```bash
dotnet run
```

### الطريقة 3: استخدام dotnet ef (للمستقبل)

إذا كنت تريد استخدام Migrations (مُوصى بها للإنتاج):

1. تثبيت dotnet-ef tool:
```bash
dotnet tool install --global dotnet-ef
```

2. إنشاء Migration:
```bash
dotnet ef migrations add AddWalletTables
```

3. تطبيق Migration:
```bash
dotnet ef database update
```

## سلسلة الاتصال

تأكد من أن سلسلة الاتصال في `appsettings.json` صحيحة:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=TripWingsDB;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

## الجداول الجديدة

بعد إعادة إنشاء قاعدة البيانات، ستكون الجداول التالية متاحة:
- `UserWallets` - محافظ المستخدمين
- `WalletTransactions` - معاملات المحفظة
- `BankWithdrawals` - طلبات السحب من البنك
- `Payments` - المدفوعات (تم تحديثها)

## ملاحظات

- `EnsureCreatedAsync()` لا يضيف جداول جديدة إذا كانت قاعدة البيانات موجودة
- للإنتاج، استخدم `Database.MigrateAsync()` بدلاً من `EnsureCreatedAsync()`
- بعد إعادة إنشاء قاعدة البيانات، سيتم تلقائياً:
  - إنشاء الأدوار (Admin, User)
  - إنشاء مستخدم Admin (admin@tripwings.com / Admin@123)
  - إضافة 28 رحلة تجريبية
  - إضافة صور وخصومات
