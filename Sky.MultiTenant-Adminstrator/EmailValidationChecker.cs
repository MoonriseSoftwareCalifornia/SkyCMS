namespace Cosmos.MultiTenant.Administrator
{
    /// <summary>
    /// EmailHeaderAnalyzer class provides methods to analyze email headers for spam detection.
    /// </summary>
    public class EmailHeaderAnalyzer
    {
        /// <summary>
        /// Checks if the email headers indicate that the email is not likely to be spam.
        /// </summary>
        /// <param name="headers"></param>
        /// <returns>True or false.</returns>
        public static bool IsNotSpam(Dictionary<string, string> headers)
        {
            // Extract key headers
            if (!headers.TryGetValue("X-Forefront-Antispam-Report", out string? antispamReport) ||
             !headers.TryGetValue("Authentication-Results", out string? authResults))
            {
                return false; // If either header is missing, we cannot determine spam likelihood
            }

            if (string.IsNullOrEmpty(antispamReport) || string.IsNullOrEmpty(authResults))
            {
                return false; // If either report is empty, we cannot determine spam likelihood
            }

            // Check SFV (Spam Filtering Verdict)
            bool sfvSafe = antispamReport?.Contains("SFV:NSPM") == true ||
                           antispamReport?.Contains("SFV:SKN") == true ||
                           antispamReport?.Contains("SFV:SKS") == true;

            // Check SCL (Spam Confidence Level)
            bool sclSafe = false;
            if (antispamReport != null && antispamReport.Contains("SCL:"))
            {
                int sclIndex = antispamReport.IndexOf("SCL:") + 4;
                if (int.TryParse(antispamReport.Substring(sclIndex, 1), out int scl))
                {
                    sclSafe = scl <= 1;
                }
            }

            // Check SPF, DKIM, and DMARC
            bool spfPass = authResults?.Contains("spf=pass") == true;
            bool dkimPass = authResults?.Contains("dkim=pass") == true;
            bool dmarcPass = authResults?.Contains("dmarc=pass") == true;

            return sfvSafe || sclSafe || spfPass || dkimPass || dmarcPass;
        }
    }
}
