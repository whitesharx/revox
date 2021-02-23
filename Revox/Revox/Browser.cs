// Copyright (c) 2021 Sergey Ivonchik
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
// OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;
using Serilog;
using WhiteSharx.Ci.Revox.Extensions;

namespace WhiteSharx.Ci.Revox {
  public enum LoginResult {
    Success,
    Failure,
    TwoFactor
  }

  public class Browser : IDisposable {
    private const string UnityIdUrl = "https://id.unity.com";
    private const string UnityLicensesUrl = "https://id.unity.com/en/subscriptions";

    private readonly RemoteWebDriver driver;
    private readonly Context context;
    private readonly ILogger logger;

    public Browser(Context ctx) {
      context = ctx;
      logger = ctx.Logger.ForContext<Browser>();

      var driverOptions = new FirefoxOptions();
      driverOptions.AddArgument("-headless");
      driverOptions.SetPreference("browser.privatebrowsing.autostart", true);
      driverOptions.LogLevel = FirefoxDriverLogLevel.Fatal;

      driver = new FirefoxDriver(driverOptions);
      driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(4);
    }

    public LoginResult Login() {
      driver.Navigate().GoToUrl(UnityIdUrl);

      logger.Information("[Login ({Login})] Url: {Url} Title: {Title}",
        context.Unity.Login, driver.Url, driver.Title);

      var emailElement = driver.FindElement(By.Id("conversations_create_session_form_email"));
      var passwordElement = driver.FindElement(By.Id("conversations_create_session_form_password"));
      var signInButton = driver.FindElement(By.CssSelector("input.btn.bg-gr"));

      emailElement.SendKeys(context.Unity.Login);
      passwordElement.SendKeys(context.Unity.Password);

      signInButton.Click();

      var verifyElement = driver.FindElementUnsafe(By.Id("conversations_email_tfa_required_form_code"));
      var failureElement = driver.FindElementUnsafe(By.CssSelector("div.error-msg"));

      if (null != verifyElement) {
        logger.Information("[Login] Verification required... Stopping.");
        return LoginResult.TwoFactor;
      }

      if (null != failureElement) {
        logger.Information("[Login] Authorization failure... Stopping.");
        return LoginResult.Failure;
      }

      return LoginResult.Success;
    }

    public bool ApplyTwoFactor(string code) {
      logger.Information("[Login] Applying verification code {Code}", code);

      var verifyElement = driver.FindElement(By.Id("conversations_email_tfa_required_form_code"));
      var verifyButton = driver.FindElement(By.CssSelector("input.btn.bg-gr"));

      verifyElement.SendKeys(code);
      verifyButton.Click();

      var failureElement = driver.FindElementUnsafe(By.CssSelector("div.error-msg"));
      var isDisplayed = null != failureElement && failureElement.Displayed;

      return !isDisplayed;
    }

    public int Count() {
      driver.Navigate().GoToUrl(UnityLicensesUrl);
      logger.Information("[Count] Url: {Url} Title: {Title}", driver.Url, driver.Title);

      var totalTextElement = driver.FindElementUnsafe(By.CssSelector("span.left.pt5.ml20"));
      return null == totalTextElement ? 0 : ParseActivationsCount(totalTextElement);
    }

    public bool TryRevoke() {
      var currentCount = Count();

      if (0 == currentCount) {
        logger.Information("[Deactivate] Nothing to deactivate. Skipping...");
        return true;
      }

      logger.Information("[Deactivate] Revoking {Count} Activations", currentCount);

      var revokeElement = driver.FindElement(By.CssSelector("input.btn.s.outlined.mr15.mb15.right"));
      revokeElement.Click();

      var nextCount = Count();

      if (0 == nextCount) {
        return true;
      }

      logger.Error("[Deactivate] Failed to revoke {Count} activations. {Next} left.", currentCount, nextCount);
      return false;
    }

    private static int ParseActivationsCount(IWebElement element) {
      var text = element.Text;

      var countText = text
        .Substring(0, text.IndexOf(",", StringComparison.Ordinal))
        .Replace("Total", string.Empty)
        .Trim();

      int.TryParse(countText, out var result);

      return result;
    }

    public void Dispose() {
      driver?.Dispose();
    }
  }
}
