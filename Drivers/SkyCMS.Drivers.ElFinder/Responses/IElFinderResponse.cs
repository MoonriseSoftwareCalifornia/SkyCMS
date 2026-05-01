// <copyright file="IElFinderResponse.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace SkyCMS.Drivers.ElFinder.Responses
{
    /// <summary>
    /// Base marker interface for all elFinder CQRS response types.
    /// </summary>
    /// <remarks>
    /// All elFinder command responses implement this interface, enabling centralized serialization
    /// to JSON and response formatting that complies with the elFinder 2.1 API specification.
    /// </remarks>
    public interface IElFinderResponse
    {
    }

    /// <summary>
    /// Success response that carries typed data payload.
    /// </summary>
    /// <typeparam name="T">The type of the response payload.</typeparam>
    public interface IElFinderResponse<out T> : IElFinderResponse
    {
        /// <summary>
        /// Gets the response payload data.
        /// </summary>
        T Data { get; }
    }

    /// <summary>
    /// Error response indicating a command failed.
    /// </summary>
    public sealed class ElFinderErrorResponse : IElFinderResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ElFinderErrorResponse"/> class.
        /// </summary>
        /// <param name="errorCode">The elFinder error code (e.g., "errAccess", "errUnknownCmd").</param>
        /// <param name="errorMessage">Optional human-readable error message.</param>
        public ElFinderErrorResponse(string errorCode, string errorMessage = null)
        {
            this.ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
            this.ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Gets the elFinder error token array as required by the elFinder 2.1 protocol.
        /// Serializes as: {"error":["errCode"]} or {"error":["errCode","message"]}
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public List<string> Error
        {
            get
            {
                var list = new List<string> { this.ErrorCode };
                if (!string.IsNullOrEmpty(this.ErrorMessage))
                    list.Add(this.ErrorMessage);
                return list;
            }
        }

        /// <summary>
        /// Gets the elFinder error code.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string ErrorCode { get; }

        /// <summary>
        /// Gets the optional error message.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string ErrorMessage { get; }

        /// <summary>
        /// Creates an "errUnknownCmd" error response.
        /// </summary>
        public static ElFinderErrorResponse UnknownCommand()
            => new("errUnknownCmd", "Unknown command");

        /// <summary>
        /// Creates an "errAccess" error response.
        /// </summary>
        public static ElFinderErrorResponse Access(string message = "Access denied")
            => new("errAccess", message);

        /// <summary>
        /// Creates an "errOpen" error response.
        /// </summary>
        public static ElFinderErrorResponse Open(string message = "Unable to open resource")
            => new("errOpen", message);

        /// <summary>
        /// Creates an "errNotFound" error response.
        /// </summary>
        public static ElFinderErrorResponse NotFound(string message = "File not found")
            => new("errNotFound", message);

        /// <summary>
        /// Creates an "errCmdParams" error response.
        /// </summary>
        public static ElFinderErrorResponse InvalidParams(string message = "Invalid command parameters")
            => new("errCmdParams", message);

        /// <summary>
        /// Creates a generic error response.
        /// </summary>
        public static ElFinderErrorResponse Generic(string errorCode, string message = null)
            => new(errorCode, message);
    }

    /// <summary>
    /// Generic success response wrapper.
    /// </summary>
    /// <typeparam name="T">The type of the response data.</typeparam>
    public sealed class ElFinderDataResponse<T> : IElFinderResponse<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ElFinderDataResponse{T}"/> class.
        /// </summary>
        /// <param name="data">The response payload.</param>
        public ElFinderDataResponse(T data)
        {
            this.Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Gets the response data.
        /// </summary>
        public T Data { get; }
    }
}
