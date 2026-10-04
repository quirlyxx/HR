
using HR.Services;
using Amazon;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// AWS S3
builder.Services.AddSingleton<S3Service>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();

    var accessKey = configuration["AWS:AccessKey"];
    var secretKey = configuration["AWS:SecretKey"];
    var bucketName = configuration["AWS:BucketName"];
    var region = configuration["AWS:Region"];

    if (string.IsNullOrWhiteSpace(accessKey))
        throw new Exception("AWS:AccessKey не задан");

    if (string.IsNullOrWhiteSpace(secretKey))
        throw new Exception("AWS:SecretKey не задан");

    if (string.IsNullOrWhiteSpace(bucketName))
        throw new Exception("AWS:BucketName не задан");

    if (string.IsNullOrWhiteSpace(region))
        throw new Exception("AWS:Region не задан");

    return new S3Service(
        accessKey,
        secretKey,
        bucketName,
        RegionEndpoint.GetBySystemName(region)
    );
});

builder.Services.AddSingleton(new GeminiService(
    builder.Configuration["Gemini:ApiKey"] ?? "",
    builder.Configuration["Gemini:Model"] ?? "gemini-3.8-flash"
));

var app = builder.Build();

// HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();