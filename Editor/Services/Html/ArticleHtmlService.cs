// <copyright file="ArticleHtmlService.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Editor.Services.Html
{
    using System;
    using HtmlAgilityPack;

    /// <summary>
    /// Concrete implementation of <see cref="IArticleHtmlService"/> using HtmlAgilityPack
    /// to safely parse and manipulate HTML fragments for editorial tooling and content analysis.
    /// </summary>
    public sealed class ArticleHtmlService : IArticleHtmlService
    {
        /// <inheritdoc />
        public string EnsureEditableMarkers(string html)
        {
            if (html == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var editable = doc.DocumentNode.SelectNodes("//*[@contenteditable='true' or translate(@contenteditable,'TRUE','true')='true' or @data-editor-config or @data-ccms-ceid]")
                              ?? new HtmlNodeCollection(null);

                // Only add markers to nodes that have contenteditable="true".
                if (editable.Count > 0)
                {
                    int i = 0;
                    foreach (var node in editable)
                    {
                        if (node.Attributes["data-ccms-ceid"] == null)
                        {
                            node.Attributes.Add("data-ccms-ceid", Guid.NewGuid().ToString("N"));
                        }

                        if (node.Attributes["data-ccms-index"] == null)
                        {
                            node.Attributes.Add("data-ccms-index", (i++).ToString());
                        }
                        else
                        {
                            node.Attributes["data-ccms-index"].Value = (i++).ToString();
                        }

                        // By now all editable areas should have Guids.
                        // Remove this.
                        node.Attributes.Remove("data-ccms-new");
                    }
                }

                return doc.DocumentNode.OuterHtml;
            }
            catch
            {
                return html;
            }
        }

        /// <inheritdoc />
        public string EnsureAngularBase(string headerFragment, string urlPath)
        {
            if (string.IsNullOrWhiteSpace(headerFragment))
            {
                return string.Empty;
            }

            var doc = new HtmlDocument();
            try { doc.LoadHtml(headerFragment); } catch { return headerFragment; }

            var meta = doc.DocumentNode.SelectSingleNode("//meta[@name='ccms:framework']");
            if (meta == null ||
                meta.Attributes["value"] == null ||
                !meta.Attributes["value"].Value.Equals("angular", System.StringComparison.OrdinalIgnoreCase))
            {
                return headerFragment;
            }

            var baseNode = doc.DocumentNode.SelectSingleNode("//base");
            var normalized = "/" + (urlPath ?? string.Empty).Trim('/').ToLowerInvariant() + "/";
            if (normalized == "//")
            {
                normalized = "/";
            }

            if (baseNode == null)
            {
                baseNode = doc.CreateElement("base");
                baseNode.SetAttributeValue("href", normalized);
                doc.DocumentNode.AppendChild(baseNode);
            }
            else
            {
                if (baseNode.Attributes["href"] == null)
                {
                    baseNode.Attributes.Add("href", normalized);
                }
                else
                {
                    baseNode.Attributes["href"].Value = normalized;
                }
            }

            return doc.DocumentNode.OuterHtml;
        }

        /// <inheritdoc />
        public string ExtractIntroduction(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var p = doc.DocumentNode.SelectSingleNode("//p[normalize-space()]");
                if (p == null)
                {
                    return string.Empty;
                }

                var text = System.Net.WebUtility.HtmlDecode(p.InnerText).Trim();
                return text.Length > 512 ? text[..512] : text;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
