using System;
using System.Runtime.Serialization;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cosmos.EmailServices.Templates;

namespace Sky.Tests
{
    [TestClass]
    public class EmailTemplateParserTests
    {
        [TestMethod]
        public void Constructor_Throws_WhenTemplateMissing()
        {
            // Use a name that is very unlikely to exist in embedded resources
            Assert.ThrowsException<ArgumentException>(() => new EmailTemplateParser("__NON_EXISTENT_TEMPLATE__"));
        }

        [TestMethod]
        public void Insert_ReplacesPlaceholder_InHtmlAndText()
        {
            // Create instance without running constructor
            var instance = (EmailTemplateParser)FormatterServices.GetUninitializedObject(typeof(EmailTemplateParser));

            // Set private backing fields for Html and Text
            SetAutoPropertyBackingField(instance, "Html", "Hello {{Name}}!");
            SetAutoPropertyBackingField(instance, "Text", "Hello {{Name}}!");

            instance.Insert("Name", "Alice");

            Assert.AreEqual("Hello Alice!", instance.Html);
            Assert.AreEqual("Hello Alice!", instance.Text);
        }

        [TestMethod]
        public void InsertHtml_InsertsHtmlAndUpdatesTextWithInnerText()
        {
            var instance = (EmailTemplateParser)FormatterServices.GetUninitializedObject(typeof(EmailTemplateParser));

            SetAutoPropertyBackingField(instance, "Html", "Body: {{Content}}");
            SetAutoPropertyBackingField(instance, "Text", "Body: {{Content}}");

            // Insert HTML that contains tags; Text replacement should get inner text
            instance.InsertHtml("Content", "<strong>Bold</strong> and <em>Em</em>");

            Assert.IsTrue(instance.Html.Contains("<strong>Bold</strong> and <em>Em</em>"));
            // InnerText should be "Bold and Em"
            Assert.IsTrue(instance.Text.Contains("Bold and Em"));
        }

        private static void SetAutoPropertyBackingField(object instance, string propertyName, string value)
        {
            var type = instance.GetType();
            // backing field name pattern: <PropertyName>k__BackingField
            var field = type.GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException($"Backing field for {propertyName} not found.");
            field.SetValue(instance, value);
        }
    }
}
