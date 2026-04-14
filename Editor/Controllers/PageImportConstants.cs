// <copyright file="PageImportConstants.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Cms.Controllers
{
    /// <summary>
    /// Page import constants.
    /// </summary>
    public static class PageImportConstants
    {
        /// <summary>
        /// Marks the start of the head injection.
        /// </summary>
        public const string COSMOSHEADSTART = "<!--  BEGIN: Cosmos Layout HEAD content inject (not editable). -->";

        /// <summary>
        /// Marks the end of the head injection.
        /// </summary>
        public const string COSMOSHEADEND = "<!--  END: Cosmos HEAD inject (not editable). -->";

        /// <summary>
        /// Marks the beginning of the optional head script injection.
        /// </summary>
        public const string COSMOSHEADSCRIPTSSTART = "<!-- BEGIN: Optional Cosmos script section injected (not editable). -->";

        /// <summary>
        /// Marks the end of the optional head script injection.
        /// </summary>
        public const string COSMOSHEADSCRIPTSEND = "<!-- END: Optional Cosmos script section injected  (not editable). -->";

        /// <summary>
        /// Marks the beginning of the header injection.
        /// </summary>
        public const string COSMOSBODYHEADERSTART = "<!-- BEGIN: Cosmos Layout BODY HEADER content (not editable) -->";

        /// <summary>
        /// Marks the end of the header injection.
        /// </summary>
        public const string COSMOSBODYHEADEREND = "<!-- END: Cosmos Layout BODY HEADER content (not editable) -->";

        /// <summary>
        /// Marks the start of the footer injection.
        /// </summary>
        public const string COSMOSBODYFOOTERSTART = "<!-- BEGIN: Cosmos Layout BODY FOOTER (not editable) -->";

        /// <summary>
        /// Marks the end of the footer injection.
        /// </summary>
        public const string COSMOSBODYFOOTEREND = "<!-- END: Cosmos Layout BODY FOOTER (not editable) -->";

        /// <summary>
        /// Marks the start of Google Translate injection.
        /// </summary>
        public const string COSMOSGOOGLETRANSLATESTART = "<!-- BEGIN: Google Translate v3 (not editable) -->";

        /// <summary>
        /// Marks the endo of Google Translate injection.
        /// </summary>
        public const string COSMOSGOOGLETRANSLATEEND = "<!-- END: Google Translate v3 (not editable) -->";

        /// <summary>
        /// Marks the start of the end-of-body script injection.
        /// </summary>
        public const string COSMOSBODYENDSCRIPTSSTART = "<!-- BEGIN: Optional Cosmos script section injected (not editable). -->";

        /// <summary>
        /// Marks the end of the end-of-body script injection.
        /// </summary>
        public const string COSMOSBODYENDSCRIPTSEND = "<!-- END: Optional Cosmos script section (not editable). -->";
    }
}
