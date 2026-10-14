# Batarka.Com – مدرسة الأحد

تطبيق ASP.NET Core MVC لإدارة حضور الخدام والمخدومين، تسجيل الدرجات، لوحات الإدارة، وإظهار أعلى 10 مخدومين.

## متطلبات التشغيل

- .NET SDK 10.0.107
- SQLite محلي للتطوير
- متصفح حديث يدعم الكاميرا عند استخدام مسح الـ QR

## التشغيل المحلي

انسخ `.env.example` إلى `.env` واضبط كلمة مرور مدير محلية قوية قبل تشغيل قاعدة جديدة. ملف `.env` مستبعد من Git.

```powershell
Copy-Item .env.example .env
dotnet restore
dotnet build
dotnet run
```

## متغيرات البيئة

- `SCHOOL_ADMIN_USERNAME` — اسم مستخدم المدير الابتدائي
- `SCHOOL_ADMIN_PASSWORD` — كلمة مرور المدير الابتدائي
- `ASPNETCORE_ENVIRONMENT` — `Development` أو `Production`
- `Database__Provider` — `Sqlite` محليًا أو `SqlServer` على الاستضافة
- `ConnectionStrings__DefaultConnection` — مطلوب عند اختيار SQL Server

لتشغيل قاعدة بيانات جديدة، عيّن `SCHOOL_ADMIN_PASSWORD` قبل التشغيل. لا توجد كلمة مرور مدير افتراضية في المشروع. حسابات التجربة تُنشأ في بيئة `Development` فقط.

## إنشاء قاعدة البيانات

```powershell
dotnet ef database update
```

## اختبار المشروع

```powershell
dotnet test
```

## المراحل المطبقة

- تسجيل دخول وخروج مع Identity
- إدارة حسابات المسجلين والمعلميين والمخدومين
- تسجيل الحضور بالاسم أو الـ QR
- نموذج أسئلة الحضور مع حساب النقاط
- تسجيل الدرجات +1 / -1 / -5
- لوحة الطالب
- لوحة أعلى 10
- لوحة الإدارة
- سجل العمليات

## ملاحظات

- الرسائل والمداخلات في الواجهة باللغة المصرية العامية.
- قاعدة البيانات تستعمل SQLite في التطوير وSQL Server في الإنتاج.
- التطبيق يضع نقاط الدرجات داخل قاعدة البيانات ويُحدّث الرصيد اعتمادًا على سجل المعاملات.

## النشر على MonsterASP

أضف المتغيرات دي من لوحة إعدادات الموقع، وما تحطش بيانات الاتصال الفعلية في GitHub:

```text
ASPNETCORE_ENVIRONMENT=Production
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=Server=YOUR_HOST,1433;Database=YOUR_DATABASE;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True
SCHOOL_ADMIN_USERNAME=admin@church.local
SCHOOL_ADMIN_PASSWORD=YOUR_UNIQUE_STRONG_PASSWORD
```

أول تشغيل هيطبق migrations الخاصة بـ SQL Server وينشئ المدير الابتدائي. بيانات الاعتماد المنشورة سابقًا لازم تتغير قبل استخدامها.
