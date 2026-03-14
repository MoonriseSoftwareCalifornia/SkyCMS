// <copyright file="ArticleLogicUtilities.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

namespace Cosmos.Common.Utilities;

using System.Text;
using Newtonsoft.Json;

/// <summary>
/// Utility methods extracted from ArticleLogic for JSON serialization and health checks.
/// </summary>
public static class ArticleLogicUtilities
{
    /// <summary>
    /// Deserialize a UTF-32 encoded JSON payload into a <typeparamref name="T"/> instance.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <param name="bytes">UTF-32 encoded JSON byte array.</param>
    /// <returns>A deserialized instance of <typeparamref name="T"/>.</returns>
    public static T Deserialize<T>(byte[] bytes)
    {
        var data = Encoding.UTF32.GetString(bytes);
        return JsonConvert.DeserializeObject<T>(data);
    }

    /// <summary>
    /// Serialize an object as JSON and return UTF-32 encoded bytes.
    /// </summary>
    /// <param name="obj">Object to serialize.</param>
    /// <returns>UTF-32 encoded JSON byte array.</returns>
    public static byte[] Serialize(object obj)
    {
        if (obj == null)
        {
            return null;
        }

        return Encoding.UTF32.GetBytes(JsonConvert.SerializeObject(obj));
    }

    /// <summary>
    /// Health probe: returns true when publisher logic layer is available.
    /// </summary>
    /// <returns>Gets the health status of the publisher logic layer.</returns>
    public static bool GetPublisherHealth() => true;
}
