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

// ReSharper disable StringLiteralTypo

using System;
using System.Threading.Tasks;
using Polly;
using WhiteSharx.Ci.Revox.Exceptions;

namespace WhiteSharx.Ci.Revox {
  public static class Program {
    public static void Main(string[] args) {
      var context = new Context(args);
      var browser = new Browser(context);

      Policy
        .Handle<RevokeFailedException>(e => {
          context.Logger.Warning("[Revox] Retrying... Exception: {Exception}", e);
          return true;
        })
        .Retry(context.Unity.RetryCount)
        .Execute(() => {
          TryRevoke(context, browser);
        });
    }

    private static void TryRevoke(Context context, Browser browser) {
      var result = browser.Login();
      var login = context.Unity.Login;

      if (LoginResult.Success == result) {
        context.Logger.Information("[Revox ({Login})] Login Successful. Deactivating...", login);

        if (browser.TryRevoke()) {
          return;
        }

        throw new RevokeFailedException();
      }

      if (LoginResult.TwoFactor != result) {
        throw new RevokeFailedException($"[Revox ({login})] Can't authorize.");
      }
      
      var email = context.Email.Login;
      context.Logger.Information("[Revox] Got two-factor verification. Applying... {Email}", email);
      
      var retryDelay = TimeSpan.FromSeconds(context.Email.RetryDelaySeconds);

      Policy.Handle<TwoFactorFailedException>(e => {
        context.Logger.Warning("[Revox] Retrying two-factor... Exception: {Exception}", e);
        return true;
      })
      .WaitAndRetry(context.Email.RetryCount, i => retryDelay)
      .Execute(() => {
        Task.Delay(TimeSpan.FromSeconds(4)).Wait();
        TryAuthorize(context, browser);

        if (browser.TryRevoke()) {
          return;
        }

        throw new RevokeFailedException();
      });
    }

    private static void TryAuthorize(Context context, Browser browser) {
      var twoFactor = new TwoFactor(context);
      var code = twoFactor.FetchLatest();

      if (string.IsNullOrEmpty(code)) {
        throw new TwoFactorFailedException("Can't fetch two factor code.");
      }

      if (!browser.ApplyTwoFactor(code)) {
        throw new TwoFactorFailedException("Can't apply two factor.");
      }
    }
  }
}
