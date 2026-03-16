using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AspNetCore.Identity.FlexDb
{
    using System;

    //https://stackoverflow.com/questions/1563191/cleanest-way-to-write-retry-logic

    /// <summary>
    /// Generic retry logic.
    /// </summary>
    /// <remarks>
    /// This class is marked as obsolete and will be removed in a future version.
    /// No usage found in the codebase. Consider using modern retry policies from Polly or built-in resilience features.
    /// See: https://learn.microsoft.com/en-us/dotnet/core/resilience/
    /// </remarks>
    [Obsolete("This class is unused and will be removed in a future version. Consider using Polly or built-in .NET resilience features.", false)]
    public static class Retry
    {
        /// <summary>
        /// Do action
        /// </summary>
        /// <param name="action"></param>
        /// <param name="retryInterval"></param>
        /// <param name="maxAttemptCount"></param>
        public static void Do(
            Action action,
            TimeSpan retryInterval,
            int maxAttemptCount = 5)
        {
            var exceptions = new List<Exception>();

            for (var attempted = 0; attempted < maxAttemptCount; attempted++)
            {
                try
                {
                    if (attempted > 0)
                    {
                        Thread.Sleep(retryInterval);
                    }

                    action();
                    return;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            throw new AggregateException(exceptions);
        }

        /// <summary>
        /// Do action
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        /// <param name="retryInterval"></param>
        /// <param name="maxAttemptCount"></param>
        /// <returns></returns>
        /// <exception cref="AggregateException"></exception>
        public static T Do<T>(
            Func<T> action,
            TimeSpan retryInterval,
            int maxAttemptCount = 5)
        {
            var exceptions = new List<Exception>();

            for (var attempted = 0; attempted < maxAttemptCount; attempted++)
                try
                {
                    if (attempted > 0) Thread.Sleep(retryInterval);
                    return action();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

            throw new AggregateException(exceptions);
        }

        /// <summary>
        /// Do async action.
        /// </summary>
        /// <param name="action">Async action to execute.</param>
        /// <param name="retryInterval">Delay between retries.</param>
        /// <param name="maxAttemptCount">Maximum attempts.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task DoAsync(
            Func<Task> action,
            TimeSpan retryInterval,
            int maxAttemptCount = 5,
            CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();

            for (var attempted = 0; attempted < maxAttemptCount; attempted++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (attempted > 0)
                    {
                        await Task.Delay(retryInterval, cancellationToken);
                    }

                    await action();
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    exceptions.Add(ex);
                }
            }

            throw new AggregateException(exceptions);
        }
    }
}
