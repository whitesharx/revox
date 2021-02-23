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

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using Serilog;

namespace WhiteSharx.Ci.Revox {
  public class TwoFactor {
    private const string UnityTitle = "Verification code for Unity ID";
    private static readonly Regex CodeRegex = new Regex(@"code\ is\ ([0-9]+)");

    private readonly Context context;
    private readonly ILogger logger;

    public TwoFactor(Context ctx) {
      context = ctx;
      logger = ctx.Logger.ForContext<TwoFactor>();
    }

    public string FetchLatest() {
      using var client = new ImapClient();
      client.Connect(context.Email.Host, context.Email.Port, true);
      client.Authenticate(context.Email.Login, context.Email.Password);

      var inbox = client.Inbox;
      inbox.Open(FolderAccess.ReadOnly);
      
      var count = inbox.Count;
      var messages = new List<MimeMessage>();
      
      if (0 == count) {
        logger.Information("[TwoFactor] No messages found. Returning...");
        return string.Empty;
      }
      
      for (var i = 0; i < count; i++) {
        messages.Add(inbox.GetMessage(i));
      }

      logger.Information("Found {Count} messages", count);

      var lastVerifyMessage = messages
        .Where(m => m.Subject.Contains(UnityTitle))
        .OrderByDescending(m => m.Date)
        .ToList()
        .FirstOrDefault();

      if (null == lastVerifyMessage) {
        logger.Information("[TwoFactor] No verify message found. Returning...");
        return string.Empty;
      }

      var match = CodeRegex.Match(lastVerifyMessage.TextBody);
      var codeGroup = match.Groups[1];
      var code = codeGroup.Captures.FirstOrDefault()?.Value;

      logger.Information($"[TwoFactor] Verify code is {code}");

      return code;
    }
  }
}
