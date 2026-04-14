// <copyright file="CommandResult.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using System.Collections.Generic;

namespace Cosmos.Common.Features.Shared;

/// <summary>
/// Base result for command execution with success/failure status.
/// </summary>
/// <remarks>
/// This class provides a standard result pattern for command handlers in the CQRS architecture.
/// It encapsulates the success or failure state of a command execution along with optional error information.
/// Use <see cref="CommandResult{T}"/> when the command needs to return data upon successful execution.
/// </remarks>
public class CommandResult
{
    /// <summary>
    /// Gets a value indicating whether the command execution was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message when the command execution fails.
    /// </summary>
    public string ErrorMessage { get; init; }

    /// <summary>
    /// Gets the validation errors when the command execution fails due to validation issues.
    /// </summary>
    public Dictionary<string, string[]> Errors { get; init; }

    /// <summary>
    /// Creates a successful command result.
    /// </summary>
    /// <returns>A successful command result.</returns>
    public static CommandResult Success() => new() { IsSuccess = true };

    /// <summary>
    /// Creates a failed command result with a single error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed command result.</returns>
    public static CommandResult Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };

    /// <summary>
    /// Creates a failed command result with validation errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A failed command result.</returns>
    public static CommandResult Failure(Dictionary<string, string[]> errors) =>
        new() { IsSuccess = false, Errors = errors };
}

/// <summary>
/// Result for command execution that returns data upon successful execution.
/// </summary>
/// <typeparam name="T">The type of data returned by the command.</typeparam>
/// <remarks>
/// This generic result type extends <see cref="CommandResult"/> to include a <see cref="Data"/> property
/// that contains the result of a successful command execution.
/// This is commonly used in CQRS command handlers that need to return information about the created or modified entity.
/// </remarks>
public class CommandResult<T> : CommandResult
{
    /// <summary>
    /// Gets the data returned by the successful command execution.
    /// </summary>
    public T Data { get; init; }

    /// <summary>
    /// Creates a successful command result with data.
    /// </summary>
    /// <param name="data">The data to return.</param>
    /// <returns>A successful command result with data.</returns>
    public static CommandResult<T> Success(T data) =>
        new() { IsSuccess = true, Data = data };

    /// <summary>
    /// Creates a failed command result with a single error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed command result.</returns>
    public static new CommandResult<T> Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };

    /// <summary>
    /// Creates a failed command result with validation errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A failed command result.</returns>
    public static new CommandResult<T> Failure(Dictionary<string, string[]> errors) =>
        new() { IsSuccess = false, Errors = errors };
}
