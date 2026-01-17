# TripWings - Travel Agency Service

مشروع ASP.NET Core MVC لإدارة خدمات وكالة السفر مع نظام صلاحيات للمدير والمستخدم.

## المميزات

### للمستخدمين (Users):
- عرض الباقات المتاحة
- إنشاء حجوزات (حد أقصى 3 حجوزات قادمة)
- إدارة قائمة الانتظار
- إضافة تقييمات للباقات
- إدارة المدفوعات
- عرض تاريخ الحجوزات والمدفوعات

### للمديرين (Admins):
- إدارة الباقات (إنشاء، تعديل، حذف)
- إدارة الحجوزات (عرض، تأكيد، إلغاء)
- إدارة الخصومات
- الموافقة على التقييمات

## القيود والأمان

1. **حد أقصى 3 حجوزات قادمة**: لا يمكن للمستخدم أن يكون لديه أكثر من 3 حجوزات مؤكدة قادمة
2. **عدم تخزين رقم البطاقة**: يتم حفظ آخر 4 أرقام فقط للرجوع إليها
3. **نظام الصلاحيات**: فصل كامل بين صلاحيات المدير والمستخدم

## التقنيات المستخدمة

- ASP.NET Core 8.0 MVC
- Entity Framework Core
- ASP.NET Core Identity
- SQL Server
- Bootstrap 5

## الإعداد والتشغيل

### المتطلبات:
- .NET 8.0 SDK
- SQL Server (LocalDB أو SQL Server Express)

### خطوات التشغيل:

1. استنساخ المشروع:
```bash
git clone <repository-url>
cd TripWings
```

2. تحديث سلسلة الاتصال في `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=TripWingsDB;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

3. إعداد إعدادات البريد الإلكتروني في `appsettings.json`:
```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "your-email@gmail.com",
    "SenderName": "TripWings",
    "Username": "your-email@gmail.com",
    "Password": "your-app-password"
  }
}
```

4. إنشاء قاعدة البيانات:
```bash
dotnet restore
dotnet ef migrations add InitialCreate
dotnet ef database update
```

5. **تشغيل المشروع مع HTTPS (مطلوب):**

   **الطريقة 1: من Visual Studio**
   - افتح المشروع في Visual Studio
   - اختر Profile "https" من قائمة التشغيل
   - أو اضغط F5 (سيستخدم HTTPS افتراضيًا)

   **الطريقة 2: من Command Line**
   ```bash
   dotnet run --launch-profile https
   ```
   
   أو قم بتعديل `Properties/launchSettings.json`:
   ```json
   {
     "profiles": {
       "https": {
         "commandName": "Project",
         "applicationUrl": "https://localhost:5001;http://localhost:5000"
       }
     }
   }
   ```

   **الطريقة 3: تعيين HTTPS كافتراضي**
   - في Visual Studio: Project Properties > Debug > Launch Settings
   - اختر "https" كـ Launch Profile الافتراضي

6. الوصول للتطبيق:
   - HTTPS: `https://localhost:5001`
   - HTTP: `http://localhost:5000` (سيتم إعادة التوجيه تلقائيًا لـ HTTPS)

7. تسجيل الدخول كمدير:
   - البريد الإلكتروني: `admin@tripwings.com`
   - كلمة المرور: `Admin@123`

### ⚠️ ملاحظة مهمة حول HTTPS:

**HTTPS مطلوب للتطوير والإنتاج** للأسباب التالية:
- أمان معالجة بيانات البطاقات (حتى في التطوير)
- متطلبات المتصفحات الحديثة
- أفضل الممارسات الأمنية

إذا واجهت مشكلة في شهادة SSL المحلية:
```bash
dotnet dev-certs https --trust
```

لإعادة إنشاء الشهادة:
```bash
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

## هيكل المشروع

```
TripWings/
├── Controllers/
│   ├── Admin/          # Controllers للمدير
│   ├── AccountController.cs
│   ├── BookingsController.cs
│   ├── PackagesController.cs
│   ├── PaymentsController.cs
│   ├── ReviewsController.cs
│   └── WaitingListController.cs
├── Data/
│   ├── ApplicationDbContext.cs
│   └── SeedData.cs
├── Models/
│   ├── ApplicationUser.cs
│   ├── Booking.cs
│   ├── Discount.cs
│   ├── Package.cs
│   ├── Payment.cs
│   ├── Review.cs
│   └── WaitingList.cs
├── Services/
│   ├── EmailService.cs
│   └── IEmailService.cs
└── Views/
    ├── Admin/          # Views للمدير
    ├── Account/
    ├── Bookings/
    ├── Packages/
    ├── Payments/
    ├── Reviews/
    └── WaitingList/
```

## قاعدة البيانات

يتم إنشاء قاعدة البيانات تلقائياً عند أول تشغيل للمشروع. يتم أيضاً إنشاء:
- أدوار المستخدمين (Admin, User)
- مستخدم مدير افتراضي
- بيانات تجريبية للباقات

## ملاحظات

- تأكد من تكوين إعدادات البريد الإلكتروني بشكل صحيح لإرسال الإشعارات
- يمكن تعديل كلمة مرور المدير الافتراضية من خلال SeedData
- المشروع يستخدم RTL (Right-to-Left) للدعم الكامل للغة العربية

## الترخيص

هذا المشروع مفتوح المصدر ومتاح للاستخدام التعليمي والتجاري.
