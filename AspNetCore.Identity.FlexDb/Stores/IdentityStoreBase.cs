using AspNetCore.Identity.FlexDb.Contracts;
using Microsoft.AspNetCore.Identity;
using System;

namespace AspNetCore.Identity.FlexDb.Stores
{
    /// <summary>
    /// Identity store base.
    /// </summary>
    public abstract class IdentityStoreBase
    {
        protected IdentityStoreBase(IRepository repo)
        {
            ArgumentNullException.ThrowIfNull(repo);
        }

        /// <summary>
        /// Processes exceptions thrown by a store method.
        /// </summary>
        /// <param name="e">Exception raised by the store operation.</param>
        /// <returns>A failed <see cref="IdentityResult"/> containing mapped identity error details.</returns>
        protected IdentityResult ProcessExceptions(Exception e)
            => IdentityResult.Failed(CreateIdentityError(e));

        /// <summary>
        /// Creates a failed identity result from explicit code and description.
        /// </summary>
        protected static IdentityResult Fail(string code, string description)
            => IdentityResult.Failed(new IdentityError { Code = code, Description = description });

        /// <summary>
        /// Maps an exception to an <see cref="IdentityError"/>.
        /// </summary>
        protected static IdentityError CreateIdentityError(Exception e)
        {
            if (e is Microsoft.Azure.Cosmos.CosmosException cosmosException)
            {
                return new IdentityError
                {
                    Code = ((int)cosmosException.StatusCode).ToString(),
                    Description = cosmosException.Message
                };
            }

            return new IdentityError
            {
                Code = "500",
                Description = e.Message
            };
        }
    }
}
