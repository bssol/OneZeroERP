using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using OneZeroErp.Domain.GeneralLedger.Setup;
using OneZeroErp.Infrastructure.GeneralLedger;
using OneZeroErp.Infrastructure.Persistence;
using OneZeroErp.IntegrationTests;
using OneZeroErp.Web;
using Xunit.Abstractions;

namespace OneZeroErp.ComponentTests;

public sealed class BootstrapBrowserTests(SqlFixture fixture, ITestOutputHelper output) : IClassFixture<SqlFixture>
{
    [Fact]
    public async Task Administrator_can_prepare_demo_data_from_the_application()
    {
        var demoFixture = new SqlFixture();
        try
        {
            await demoFixture.InitializeAsync();
            var user = await demoFixture.AddUserAsync("identity.users:CanEdit");
            using var factory = new BrowserFactory(demoFixture.Configuration);
            factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps()));
            using var client = factory.CreateClient();
            var origin = client.BaseAddress!.GetLeftPart(UriPartial.Authority);
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new()
            {
                Headless = true,
                Channel = Environment.GetEnvironmentVariable("ONEZERO_BROWSER_CHANNEL") ?? (OperatingSystem.IsWindows() ? "msedge" : null),
                Args = ["--allow-insecure-localhost"]
            });
            await using var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
            var page = await context.NewPageAsync();
            page.PageError += (_, error) => output.WriteLine(error);
            await page.GotoAsync(origin + "/login");
            await page.GetByLabel("Username", new() { Exact = true }).FillAsync(user.UserName);
            await page.GetByLabel("Password", new() { Exact = true }).FillAsync(demoFixture.Password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();

            await page.GotoAsync(origin + "/admin/demo-data");
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demo data", Exact = true })).ToBeVisibleAsync();
            await page.WaitForTimeoutAsync(1000);
            await page.GetByLabel("I understand that demo records will be added to this company.").CheckAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Create demo data", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByText("Walkthrough data is ready.", new() { Exact = false })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Walkthrough ready", new() { Exact = true })).ToBeVisibleAsync();

            await using var verify = demoFixture.CreateDbContext();
            Assert.Equal(3, await verify.Journals.CountAsync(x => x.Description.StartsWith("[DEMO]") && x.Status == "Draft"));
        }
        finally
        {
            await demoFixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task Browser_fast_payment_saves_balanced_voucher_draft()
    {
        var user = await fixture.AddUserAsync("gl.vouchers:CanView", "gl.vouchers:CanAdd");
        var date = DateOnly.FromDateTime(fixture.Clock.CurrentDateTime.DateTime);
        var year = new Erp_SetupFiscalYearEntity
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            Code = "FYBROWSER",
            StartDate = date.AddDays(-1),
            EndDate = date.AddDays(1),
            Status = FiscalYearStatus.Open,
            CreatedAtUtc = fixture.Clock.UtcNow,
            UpdatedAtUtc = fixture.Clock.UtcNow
        };
        var type = new GL_VoucherTypeEntity
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            Code = "BPV",
            Description = "Bank Payment",
            RequiresBankAccount = true,
            IsActive = true,
            CreatedAtUtc = fixture.Clock.UtcNow,
            UpdatedAtUtc = fixture.Clock.UtcNow
        };
        var currency = new GL_CurrencyEntity
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            Code = "TST",
            Name = "Test currency",
            Symbol = "T",
            DecimalPlaces = 2,
            IsBaseCurrency = true,
            IsActive = true,
            CreatedAtUtc = fixture.Clock.UtcNow,
            UpdatedAtUtc = fixture.Clock.UtcNow
        };
        GL_ChartOfAccountEntity Account(string no) => new()
        {
            Id = Guid.NewGuid(),
            AccountNo = no,
            Title = no,
            AccountType = AccountType.Asset,
            IsPostingAccount = true,
            IsActive = true,
            CreatedAtUtc = fixture.Clock.UtcNow,
            UpdatedAtUtc = fixture.Clock.UtcNow
        };
        var bankGl = Account("BROWSERBANK");
        var counterpart = Account("BROWSEROTHER");
        await using (var db = fixture.CreateDbContext())
        {
            db.FiscalYears.Add(year);
            db.LockDates.Add(new()
            {
                Id = Guid.NewGuid(),
                CompanyId = fixture.CompanyId,
                FiscalYearId = year.Id,
                Date = date,
                CreatedAtUtc = fixture.Clock.UtcNow,
                UpdatedAtUtc = fixture.Clock.UtcNow
            });
            db.VoucherTypes.Add(type); db.Currencies.Add(currency);
            db.ChartOfAccounts.AddRange(bankGl, counterpart);
            db.BankAccounts.Add(new()
            {
                Id = Guid.NewGuid(),
                CompanyId = fixture.CompanyId,
                GlAccountId = bankGl.Id,
                Code = "BROWSER",
                Name = "Test bank",
                BankName = "Test bank",
                AccountNumber = "Synthetic",
                IsActive = true,
                CreatedAtUtc = fixture.Clock.UtcNow,
                UpdatedAtUtc = fixture.Clock.UtcNow
            });
            await db.SaveChangesAsync();
        }
        using var factory = new BrowserFactory(fixture.Configuration);
        factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps()));
        using var client = factory.CreateClient();
        var origin = client.BaseAddress!.GetLeftPart(UriPartial.Authority);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Channel = Environment.GetEnvironmentVariable("ONEZERO_BROWSER_CHANNEL") ?? (OperatingSystem.IsWindows() ? "msedge" : null),
            Args = ["--allow-insecure-localhost"]
        });
        await using var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => output.WriteLine(error);
        await page.GotoAsync(origin + "/login");
        await page.GetByLabel("Username", new() { Exact = true }).FillAsync(user.UserName);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(fixture.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await page.GotoAsync(origin + "/gl/vouchers");
        await page.WaitForTimeoutAsync(1000);
        await page.GetByRole(AriaRole.Button, new() { Name = "New voucher" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await page.GetByLabel("Voucher type").SelectOptionAsync(type.Id.ToString());
        await page.GetByLabel("Voucher date").FillAsync(date.ToString("yyyy-MM-dd"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Fast payment" }).ClickAsync();
        await page.GetByLabel("Bank account").SelectOptionAsync(bankGl.Id.ToString());
        var accountPicker = page.GetByRole(AriaRole.Button, new() { Name = "Account for voucher line 1", Exact = true });
        await accountPicker.ClickAsync();
        var accountSearch = page.GetByLabel("Search chart of accounts", new() { Exact = true });
        await accountSearch.FillAsync("OTHER");
        var accountListbox = page.GetByRole(AriaRole.Listbox, new() { Name = "Account for voucher line 1", Exact = true });
        var counterpartOption = accountListbox.GetByRole(AriaRole.Option).First;
        await Assertions.Expect(counterpartOption).ToBeVisibleAsync();
        await Assertions.Expect(counterpartOption).ToContainTextAsync("BROWSEROTHER — BROWSEROTHER");
        await Assertions.Expect(page.Locator(".entity-dropdown__option strong").First).ToHaveTextAsync("OTHER");
        await counterpartOption.ClickAsync();
        await Assertions.Expect(accountSearch).ToBeHiddenAsync();
        await accountPicker.ClickAsync();
        await page.GetByLabel("Line narration").First.ClickAsync();
        await Assertions.Expect(accountSearch).ToBeHiddenAsync();
        await page.GetByLabel("Amount").First.FillAsync("12.25");
        await page.GetByRole(AriaRole.Button, new() { Name = "Remove line" }).Last.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Save draft" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Voucher BPV-FYBROWSER-000001 saved as a draft.")).ToBeVisibleAsync();
        await using var verify = fixture.CreateDbContext();
        var journal = await verify.Journals.Include(x => x.Lines).SingleAsync(x => x.Number == "BPV-FYBROWSER-000001");
        Assert.Equal("Draft", journal.Status);
        Assert.Equal(12.25m, journal.Lines.Sum(x => x.Debit));
        Assert.Equal(12.25m, journal.Lines.Sum(x => x.Credit));
    }

    [Fact]
    public async Task Browser_login_logout_offline_and_public_cache_work_without_storing_financial_data()
    {
        using var factory = new BrowserFactory(fixture.Configuration);
        factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps()));
        using var client = factory.CreateClient();
        var origin = client.BaseAddress!.GetLeftPart(UriPartial.Authority);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Channel = Environment.GetEnvironmentVariable("ONEZERO_BROWSER_CHANNEL") ?? (OperatingSystem.IsWindows() ? "msedge" : null),
            Args = ["--allow-insecure-localhost"]
        });
        await using var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();
        page.Response += (_, response) => { if (response.Status >= 400) output.WriteLine($"HTTP {response.Status}: {new Uri(response.Url).AbsolutePath}"); };
        page.PageError += (_, error) => output.WriteLine(error);
        var user = await fixture.AddUserAsync();
        await page.GotoAsync(origin + "/login");
        await page.GetByLabel("Username", new() { Exact = true }).FillAsync(user.UserName);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(fixture.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(origin + "/");
        var cookie = Assert.Single(await context.CookiesAsync(), x => x.Name.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        await page.EvaluateAsync("async () => { await navigator.serviceWorker.register('/service-worker.js'); }");
        await page.WaitForFunctionAsync("() => navigator.serviceWorker.controller !== null", null, new() { Timeout = 15000 });
        await page.ReloadAsync();
        var cdp = await context.NewCDPSessionAsync(page);
        var manifest = await cdp.SendAsync("Page.getAppManifest");
        Assert.Empty(manifest!.Value.GetProperty("errors").EnumerateArray());
        await page.EvaluateAsync("() => { window.bootstrapUpdateSentinel = true; }");
        factory.UpdateVersion++;
        await page.EvaluateAsync("async () => { const registration = await navigator.serviceWorker.getRegistration(); await registration.update(); }");
        var updateButton = page.GetByRole(AriaRole.Button, new() { Name = "Reload to update" });
        await Assertions.Expect(updateButton).ToBeVisibleAsync();
        Assert.True(await page.EvaluateAsync<bool>("() => window.bootstrapUpdateSentinel === true"));
        await updateButton.ClickAsync();
        await page.WaitForFunctionAsync("() => window.bootstrapUpdateSentinel === undefined");
        var cachedUrls = await page.EvaluateAsync<string[]>("async () => (await Promise.all((await caches.keys()).map(async name => (await (await caches.open(name)).keys()).map(request => new URL(request.url).pathname)))).flat()");
        Assert.Contains("/offline.html", cachedUrls);
        Assert.DoesNotContain(cachedUrls, x => x.StartsWith("/api", StringComparison.Ordinal) || x.StartsWith("/gl", StringComparison.Ordinal) || x == "/" || x == "/login");
        var localKeys = await page.EvaluateAsync<string[]>("() => Object.keys(localStorage)");
        Assert.All(localKeys, key => Assert.Contains(key, new[] { "onezero-theme", "onezero-sidebar-collapsed" }));
        await context.SetOfflineAsync(true);
        await page.GotoAsync(origin + "/offline-check");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "You are offline" })).ToBeVisibleAsync();
        await context.SetOfflineAsync(false);
        await page.GotoAsync(origin + "/");
        Assert.Equal(400, (await context.APIRequest.PostAsync(origin + "/account/logout")).Status);
        await page.Locator("details.profile-menu summary").ClickAsync();
        await page.Locator("form[action='/account/logout'] button").ClickAsync();
        await page.WaitForURLAsync(origin + "/login");
        Assert.DoesNotContain(await context.CookiesAsync(), x => x.Name.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
        await page.GotoAsync(origin + "/api/me");
        Assert.DoesNotContain(user.DisplayName, await page.ContentAsync());
    }

    private sealed class BrowserFactory(IConfiguration configuration) : WebApplicationFactory<WebHostMarker>
    {
        public int UpdateVersion { get; set; }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseStaticWebAssets();
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(new UpdateFilter(this)));
        }
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddConfiguration(configuration));
            return base.CreateHost(builder);
        }
    }

    // Serve the real worker with a changed version comment to exercise browser
    // update lifecycle without modifying production files or installing a mock worker.
    private sealed class UpdateFilter(BrowserFactory factory) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                if (context.Request.Path == "/service-worker.js")
                {
                    var environment = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
                    using var reader = new StreamReader(environment.WebRootFileProvider.GetFileInfo("service-worker.js").CreateReadStream());
                    context.Response.ContentType = "text/javascript";
                    context.Response.Headers.CacheControl = "no-cache";
                    await context.Response.WriteAsync(await reader.ReadToEndAsync() + $"\n// Test build {factory.UpdateVersion}\n");
                    return;
                }
                await continuation(context);
            });
            next(app);
        };
    }
}
