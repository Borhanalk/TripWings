# تعليمات إنشاء جداول التعليقات / Comments Tables Migration Instructions

## المشكلة / Problem
الجداول `SiteComments` و `CommentRatings` غير موجودة في قاعدة البيانات.

## الحل / Solution

### الطريقة 1: استخدام Entity Framework Migrations (موصى بها)

1. تأكد من تثبيت dotnet-ef:
```bash
dotnet tool install --global dotnet-ef
```

2. إنشاء migration:
```bash
dotnet ef migrations add AddSiteComments
```

3. تطبيق migration على قاعدة البيانات:
```bash
dotnet ef database update
```

### الطريقة 2: استخدام SQL Script مباشرة

1. افتح SQL Server Management Studio أو أي أداة SQL
2. افتح ملف `CREATE_COMMENTS_TABLES.sql`
3. قم بتشغيل الـ script على قاعدة البيانات الخاصة بك

### الطريقة 3: التحقق من قاعدة البيانات

تأكد من أن:
- قاعدة البيانات موجودة
- Connection string صحيح في `appsettings.json`
- لديك صلاحيات لإنشاء الجداول

## بعد إنشاء الجداول

بعد إنشاء الجداول، يجب أن يعمل نظام التعليقات بشكل صحيح.
