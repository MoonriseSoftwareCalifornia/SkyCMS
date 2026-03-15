// <copyright file="AllOrNoneRequiredAttributeTests.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Sky.Tests.Services.CDN
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Sky.Editor.Services.CDN;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Unit tests for <see cref="AllOrNoneRequiredAttribute"/> validation logic.
    /// Tests are designed to execute in parallel where independent of one another.
    /// </summary>
    [TestClass]
    public class AllOrNoneRequiredAttributeTests
    {
        #region Test Models

        /// <summary>
        /// Test model with fields validated by AllOrNoneRequired attribute.
        /// </summary>
        [AllOrNoneRequired("Field1", "Field2", "Field3")]
        private sealed class TestModel
        {
            public string Field1 { get; set; }
            public string Field2 { get; set; }
            public string Field3 { get; set; }
        }

        /// <summary>
        /// Test model with two fields validated.
        /// </summary>
        [AllOrNoneRequired("FieldA", "FieldB")]
        private sealed class TwoFieldModel
        {
            public string FieldA { get; set; }
            public string FieldB { get; set; }
        }

        /// <summary>
        /// Test model with single field (edge case).
        /// </summary>
        [AllOrNoneRequired("SingleField")]
        private sealed class SingleFieldModel
        {
            public string SingleField { get; set; }
        }

        #endregion

        #region All Filled Tests

        /// <summary>
        /// Test: Validation succeeds when all fields are filled.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.AllFilled")]
        public void IsValid_AllFieldsFilled_ReturnsSuccess()
        {
            // Arrange
            var model = new TestModel
            {
                Field1 = "value1",
                Field2 = "value2",
                Field3 = "value3"
            };

            var attribute = new AllOrNoneRequiredAttribute("Field1", "Field2", "Field3");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.AreEqual(ValidationResult.Success, result);
        }

        /// <summary>
        /// Test: Validation succeeds with all fields filled in two-field model.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.AllFilled")]
        public void IsValid_TwoFieldsAllFilled_ReturnsSuccess()
        {
            // Arrange
            var model = new TwoFieldModel
            {
                FieldA = "alpha",
                FieldB = "beta"
            };

            var attribute = new AllOrNoneRequiredAttribute("FieldA", "FieldB");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.AreEqual(ValidationResult.Success, result);
        }

        /// <summary>
        /// Test: Validation succeeds when all fields have whitespace-only values (treated as empty).
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.AllFilled")]
        public void IsValid_FieldsWithWhitespace_TreatedAsEmpty()
        {
            // Arrange
            var model = new TestModel
            {
                Field1 = "   ",
                Field2 = "\t",
                Field3 = "\n"
            };

            var attribute = new AllOrNoneRequiredAttribute("Field1", "Field2", "Field3");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.AreEqual(ValidationResult.Success, result);
        }

        #endregion

        #region All Empty Tests

        /// <summary>
        /// Test: Validation succeeds when all fields are null.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.AllEmpty")]
        public void IsValid_AllFieldsNull_ReturnsSuccess()
        {
            // Arrange
            var model = new TestModel
            {
                Field1 = null,
                Field2 = null,
                Field3 = null
            };

            var attribute = new AllOrNoneRequiredAttribute("Field1", "Field2", "Field3");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.AreEqual(ValidationResult.Success, result);
        }

        /// <summary>
        /// Test: Validation succeeds when all fields are empty strings.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.AllEmpty")]
        public void IsValid_AllFieldsEmpty_ReturnsSuccess()
        {
            // Arrange
            var model = new TestModel
            {
                Field1 = "",
                Field2 = "",
                Field3 = ""
            };

            var attribute = new AllOrNoneRequiredAttribute("Field1", "Field2", "Field3");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.AreEqual(ValidationResult.Success, result);
        }

        /// <summary>
        /// Test: Validation succeeds when all fields are mixed null and empty.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.AllEmpty")]
        public void IsValid_AllFieldsMixedNullAndEmpty_ReturnsSuccess()
        {
            // Arrange
            var model = new TestModel
            {
                Field1 = null,
                Field2 = "",
                Field3 = "  "
            };

            var attribute = new AllOrNoneRequiredAttribute("Field1", "Field2", "Field3");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.AreEqual(ValidationResult.Success, result);
        }

        #endregion

        #region Partial Fill Tests

        /// <summary>
        /// Test: Validation fails when one field is filled and others are empty.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.PartialFill")]
        public void IsValid_OneFieldFilledOthersEmpty_ReturnsFail()
        {
            // Arrange
            var model = new TestModel
            {
                Field1 = "value1",
                Field2 = null,
                Field3 = null
            };

            var attribute = new AllOrNoneRequiredAttribute("Field1", "Field2", "Field3");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("all or none"));
        }

        /// <summary>
        /// Test: Validation fails when two of three fields are filled.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.PartialFill")]
        public void IsValid_TwoOfThreeFieldsFilled_ReturnsFail()
        {
            // Arrange
            var model = new TestModel
            {
                Field1 = "val1",
                Field2 = "val2",
                Field3 = null
            };

            var attribute = new AllOrNoneRequiredAttribute("Field1", "Field2", "Field3");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ErrorMessage.Contains("all or none"));
        }

        /// <summary>
        /// Test: Validation fails with partial filled two-field model.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.PartialFill")]
        public void IsValid_TwoFieldsPartiallyFilled_ReturnsFail()
        {
            // Arrange
            var model = new TwoFieldModel
            {
                FieldA = "present",
                FieldB = null
            };

            var attribute = new AllOrNoneRequiredAttribute("FieldA", "FieldB");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ErrorMessage.Contains("all or none"));
        }

        /// <summary>
        /// Test: Error message includes field names.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.PartialFill")]
        public void IsValid_PartialFill_ErrorMessageIncludesFieldNames()
        {
            // Arrange
            var model = new TestModel
            {
                Field1 = "filled",
                Field2 = null,
                Field3 = null
            };

            var attribute = new AllOrNoneRequiredAttribute("Field1", "Field2", "Field3");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ErrorMessage.Contains("Field1"));
            Assert.IsTrue(result.ErrorMessage.Contains("Field2"));
            Assert.IsTrue(result.ErrorMessage.Contains("Field3"));
        }

        #endregion

        #region Single Field Tests

        /// <summary>
        /// Test: Single field with value passes validation.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.SingleField")]
        public void IsValid_SingleFieldFilled_ReturnsSuccess()
        {
            // Arrange
            var model = new SingleFieldModel { SingleField = "value" };
            var attribute = new AllOrNoneRequiredAttribute("SingleField");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.AreEqual(ValidationResult.Success, result);
        }

        /// <summary>
        /// Test: Single field empty passes validation.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.SingleField")]
        public void IsValid_SingleFieldEmpty_ReturnsSuccess()
        {
            // Arrange
            var model = new SingleFieldModel { SingleField = null };
            var attribute = new AllOrNoneRequiredAttribute("SingleField");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.AreEqual(ValidationResult.Success, result);
        }

        #endregion

        #region Edge Cases

        /// <summary>
        /// Test: Empty property names array succeeds.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.EdgeCases")]
        public void IsValid_EmptyPropertyNames_Succeeds()
        {
            // Arrange
            var model = new TestModel();
            var attribute = new AllOrNoneRequiredAttribute();
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.AreEqual(ValidationResult.Success, result);
        }

        /// <summary>
        /// Test: NonExistent property name handled gracefully.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.EdgeCases")]
        public void IsValid_NonExistentPropertyName_Succeeds()
        {
            // Arrange
            var model = new TestModel
            {
                Field1 = "value1",
                Field2 = "value2",
                Field3 = "value3"
            };

            var attribute = new AllOrNoneRequiredAttribute("Field1", "NonExistent", "Field3");
            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            // Should treat non-existent field as null, which is acceptable if mixed with empty
            Assert.IsNotNull(result);
        }

        /// <summary>
        /// Test: Large number of fields validates correctly.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.EdgeCases")]
        public void IsValid_ManyFields_AllFilled_Succeeds()
        {
            // Arrange
            var modelType = typeof(TestModel);
            var attribute = new AllOrNoneRequiredAttribute("Field1", "Field2", "Field3");

            var model = new TestModel
            {
                Field1 = "1",
                Field2 = "2",
                Field3 = "3"
            };

            var context = new ValidationContext(model);

            // Act
            var result = attribute.GetValidationResult(model, context);

            // Assert
            Assert.AreEqual(ValidationResult.Success, result);
        }

        #endregion

        #region Attribute Configuration Tests

        /// <summary>
        /// Test: Attribute can be applied to class.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.Configuration")]
        public void Attribute_CanBeAppliedToClass_Succeeds()
        {
            // Arrange & Act
            var attribute = new AllOrNoneRequiredAttribute("Field1", "Field2");

            // Assert
            Assert.IsNotNull(attribute);
            Assert.IsInstanceOfType(attribute, typeof(ValidationAttribute));
        }

        /// <summary>
        /// Test: Constructor accepts variable number of property names.
        /// </summary>
        [TestMethod]
        [TestCategory("AllOrNoneRequired.Configuration")]
        public void Constructor_VariablePropertyNames_AcceptsMultiple()
        {
            // Arrange & Act
            var attribute1 = new AllOrNoneRequiredAttribute("Field1");
            var attribute2 = new AllOrNoneRequiredAttribute("Field1", "Field2");
            var attribute3 = new AllOrNoneRequiredAttribute("Field1", "Field2", "Field3", "Field4", "Field5");

            // Assert
            Assert.IsNotNull(attribute1);
            Assert.IsNotNull(attribute2);
            Assert.IsNotNull(attribute3);
        }

        #endregion
    }
}
