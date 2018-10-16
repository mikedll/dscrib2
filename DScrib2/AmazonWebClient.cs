﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Parser.Html;

namespace DScrib2
{
    public class AmazonWebClient
    {
        public AmazonWebClient()
        {
            this.IsTestMode = false;
        }

        public bool IsTestMode
        {
            get; set;
        }

        private string ReadFile(string file)
        {
            var body = "";
            using (var sr = new StreamReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file), Encoding.UTF8))
            {
                body += sr.ReadToEnd();
            }
            return body;
        }

        public string GetTestSearch()
        {
            return ReadFile("output.html");
        }

        public string GetTestReview()
        {
            return ReadFile("sample-review.html");
        }

        private string GetPage(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);

            string body = "";
            WebResponse response = null;
            try
            {
                response = request.GetResponse();
            }
            catch (System.Net.WebException)
            {
                return null;
            }

            if (response == null) return "";

            using (var str = response.GetResponseStream())
            {
                byte[] buf = new byte[1024];
                int n, offset = 0;
                n = str.Read(buf, offset, buf.Length);
                while (n > 0)
                {
                    body += Encoding.UTF8.GetString(buf, 0, n);
                    offset += n;
                    n = str.Read(buf, 0, buf.Length);
                }
            }
            return body;
        }


        private static AngleSharp.Dom.Html.IHtmlDocument ParseDoc(string body)
        {
            var parser = new HtmlParser();
            var doc = parser.Parse(body);
            return doc;
        }

        /*
         * Returns date, review of top-rated positive review from review page.
         * 
         */
        public Tuple<DateTime, string> GetReviewPage(string linkSlug, string productID)
        {
            // Reviews look like this: https://www.amazon.com/Eucalan-Lavender-Fine-Fabric-Ounce/product-reviews/B001DEJMPG/
            var body = GetPage($"https://www.amazon.com/{linkSlug}/product-reviews/{productID}/");

            var doc = ParseDoc(body);
            var reviewEl = doc.QuerySelector(".view-point-review.positive-review");

            if (reviewEl == null) return null;

            var dateEl = reviewEl.QuerySelector(".a-row .review-date");
            DateTime reviewDate = DateTime.Now;
            if (dateEl != null)
            {
                var reviewDateStr = dateEl.TextContent;
                if (reviewDateStr.StartsWith("on ")) {
                    CultureInfo enUs = new CultureInfo("en-US");
                    if (!DateTime.TryParseExact(reviewDateStr.Substring(3), "MMM d, yyyy", enUs, DateTimeStyles.None, out reviewDate))
                    {
                        reviewDate = DateTime.Now;
                    }
                }
            }

            var reviewWrapper = reviewEl.QuerySelector(".a-row .a-spacing-top-mini:last-child");
            var reviewNode = reviewWrapper.QuerySelector(".a-size-base");
            var review = "";
            if (reviewNode != null)
            {
                review = reviewNode.TextContent;
            }

            return new Tuple<DateTime, string>(reviewDate, review);
        }

        /*
         * Returns a (name, linkSlug, productID) for every product found.
         */
        public List<Tuple<string, string, string>> Search(string q)
        {
            const int MaxSize = 200;
            var url = "https://www.amazon.com/s/?field-keywords=" + Uri.EscapeDataString(q.Substring(0, Math.Min(q.Length, MaxSize)));
            var body = GetPage(url);
            if (body == null)
            {
                return new List<Tuple<string, string, string>>();
            }

            var doc = ParseDoc(body);
            var items = doc.QuerySelectorAll(".s-result-list .a-link-normal.s-access-detail-page");
            var n = items.Count();

            var results = new List<Tuple<string, string, string>>();

            // Products look like this: https://www.amazon.com/Sandalwood-Patchouli-Different-Scents-Karma/dp/B06Y274RR8/
            // There is a link-slug, a /dp/, and an external product ID string.
            var productUrlRegex = new Regex(@"http(s)?://www.amazon.com/([^/]+)/dp/([^/]+)/", RegexOptions.Compiled);
            foreach (var item in items)
            {
                var link = item.GetAttribute("href");
                var name = item.QuerySelector("h2").TextContent;

                var match = productUrlRegex.Match(link);

                if (match.Success)
                {
                    results.Add(new Tuple<string, string, string>(name, match.Groups[2].Value, match.Groups[3].Value));
                }
            }
            return results;
        }

    }
}