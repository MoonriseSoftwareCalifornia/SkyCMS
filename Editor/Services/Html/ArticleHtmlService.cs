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
        /// <summary>
        /// XPath query to select elements that have editable markers (data-editor-config, data-ccms-new, 
        /// data-ccms-enable-alt-editor, data-ccms-ceid, or contenteditable) but lack a valid data-ccms-ceid value.
        /// </summary>
        private const string UnmarkedEditableRegionsXPath = "//*[(@data-editor-config or @data-ccms-new or @data-ccms-enable-alt-editor or @data-ccms-ceid or @contenteditable) and (not(@data-ccms-ceid) or normalize-space(@data-ccms-ceid)='')]";

        /// <summary>
        /// XPath query to select all elements that are considered editable regions, defined as having either contenteditable='true' or a data-ccms-ceid attribute (regardless of its value).
        /// </summary>
        private const string EditableRegionsXPath = "//*[@contenteditable='true' or @data-ccms-ceid]";

        /// <summary>
        /// Normalizes HTML attribute quotes from single to double quotes.
        /// HtmlAgilityPack serializes attributes with single quotes by default; this method converts them to double quotes.
        /// </summary>
        /// <param name="html">The HTML string with single-quoted attributes.</param>
        /// <returns>The HTML string with double-quoted attributes.</returns>
        private static string NormalizeQuotesToDouble(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return html;
            }

            // Use a simple state machine to replace single quotes with double quotes only in attribute values
            // This is a simplified approach that works for well-formed HTML from HtmlAgilityPack
            var result = new System.Text.StringBuilder(html.Length);
            bool inTag = false;
            bool inAttributeValue = false;
            char quoteChar = '\0';

            for (int i = 0; i < html.Length; i++)
            {
                char c = html[i];

                if (!inTag && c == '<')
                {
                    inTag = true;
                    result.Append(c);
                }
                else if (inTag && c == '>')
                {
                    inTag = false;
                    inAttributeValue = false;
                    result.Append(c);
                }
                else if (inTag && !inAttributeValue && (c == '\'' || c == '"'))
                {
                    // Starting an attribute value
                    inAttributeValue = true;
                    quoteChar = c;
                    result.Append('"'); // Always use double quote
                }
                else if (inTag && inAttributeValue && c == quoteChar)
                {
                    // Ending an attribute value
                    inAttributeValue = false;
                    result.Append('"'); // Always use double quote
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }

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

            // Early exit if no unmarked regions - but still normalize quotes
            if (!HasUnMarkedEditableRegions(html))
            {
                return NormalizeQuotesToDouble(html);
            }

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Use the same XPath logic as HasUnMarkedEditableRegions to ensure consistency
                var editable = doc.DocumentNode.SelectNodes(UnmarkedEditableRegionsXPath)
                              ?? new HtmlNodeCollection(null);

                // Only add markers to nodes that need them.
                if (editable.Count > 0)
                {
                    int i = 0;
                    foreach (var node in editable)
                    {
                        var ceidAttr = node.Attributes["data-ccms-ceid"];
                        if (ceidAttr == null || string.IsNullOrWhiteSpace(ceidAttr.Value))
                        {
                            var guidValue = Guid.NewGuid().ToString("N");
                            if (ceidAttr == null)
                            {
                                node.Attributes.Add("data-ccms-ceid", guidValue);
                            }
                            else
                            {
                                ceidAttr.Value = guidValue;
                            }
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
                        // Remove temporary marker attributes.
                        node.Attributes.Remove("data-ccms-new");
                        node.Attributes.Remove("contenteditable");
                    }
                }

                // HtmlAgilityPack uses single quotes by default; normalize to double quotes
                var output = doc.DocumentNode.OuterHtml;
                return NormalizeQuotesToDouble(output);
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
            try
            {
                doc.LoadHtml(headerFragment);
            }
            catch
            {
                return headerFragment;
            }

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

            // HtmlAgilityPack uses single quotes by default; normalize to double quotes
            return NormalizeQuotesToDouble(doc.DocumentNode.OuterHtml);
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

        /// <inheritdoc />
        public bool HasUnMarkedEditableRegions(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return false;
            }

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var unmarked = doc.DocumentNode.SelectNodes(UnmarkedEditableRegionsXPath);
                return unmarked != null && unmarked.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public bool HasEditableRegions(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return false;
            }

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var editable = doc.DocumentNode.SelectNodes("//*[@contenteditable='true' or @data-ccms-ceid]");
                return editable != null && editable.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
