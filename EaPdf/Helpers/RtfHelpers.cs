using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace UIUCLibrary.EaPdf.Helpers
{
    public static class RtfHelpers
    {
        public static string ConvertToXhtml(TextPart txtPart, ref List<(LogLevel level, string message)> messages, bool ignoreHtmlIssues)
        {
            if (txtPart==null || !txtPart.IsRichText)
            {
                throw new ArgumentException("TextPart is not Rich Text (text/rtf)", nameof(txtPart));
            }

            var rtfSet = new RtfPipe.RtfHtmlSettings();
            rtfSet.WithFullDocument();
            var htmlText = RtfPipe.Rtf.ToHtml(txtPart.Content.Open(), rtfSet);

            //clean up the html so it is valid-ish xhtml, log any issues to the messages list
            htmlText = HtmlHelpers.ConvertHtmlToXhtml(htmlText, ref messages, ignoreHtmlIssues);

            return htmlText;
        }
    }
}
