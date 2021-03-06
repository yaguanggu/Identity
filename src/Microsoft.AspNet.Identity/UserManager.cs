// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Identity.Logging;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;

namespace Microsoft.AspNet.Identity
{
    /// <summary>
    /// Provides the APIs for managing user in a persistence store.
    /// </summary>
    /// <typeparam name="TUser">The type encapsulating a user.</typeparam>
    public class UserManager<TUser> : IDisposable where TUser : class
    {
        private readonly Dictionary<string, IUserTokenProvider<TUser>> _tokenProviders =
            new Dictionary<string, IUserTokenProvider<TUser>>();

        private TimeSpan _defaultLockout = TimeSpan.Zero;
        private bool _disposed;
        private readonly HttpContext _context;
        private CancellationToken CancellationToken => _context?.RequestAborted ?? CancellationToken.None;

        /// <summary>
        /// Constructs a new instance of <see cref="UserManager{TUser}"/>.
        /// </summary>
        /// <param name="store">The persistence store the manager will operate over.</param>
        /// <param name="optionsAccessor">The accessor used to access the <see cref="IdentityOptions"/>.</param>
        /// <param name="passwordHasher">The password hashing implementation to use when saving passwords.</param>
        /// <param name="userValidators">A collection of <see cref="IUserValidator{TUser}"/> to validate users against.</param>
        /// <param name="passwordValidators">A collection of <see cref="IPasswordValidator{TUser}"/> to validate passwords against.</param>
        /// <param name="keyNormalizer">The <see cref="ILookupNormalizer"/> to use when generating index keys for users.</param>
        /// <param name="errors">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="tokenProviders"></param>
        /// <param name="logger">The logger used to log messages, warnings and errors.</param>
        /// <param name="contextAccessor">The accessor used to access the <see cref="HttpContext"/>.</param>
        public UserManager(IUserStore<TUser> store,
            IOptions<IdentityOptions> optionsAccessor,
            IPasswordHasher<TUser> passwordHasher,
            IEnumerable<IUserValidator<TUser>> userValidators,
            IEnumerable<IPasswordValidator<TUser>> passwordValidators,
            ILookupNormalizer keyNormalizer,
            IdentityErrorDescriber errors,
            IEnumerable<IUserTokenProvider<TUser>> tokenProviders,
            ILogger<UserManager<TUser>> logger,
            IHttpContextAccessor contextAccessor)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }
            Store = store;
            Options = optionsAccessor?.Options ?? new IdentityOptions();
            _context = contextAccessor?.HttpContext;
            PasswordHasher = passwordHasher;
            KeyNormalizer = keyNormalizer;
            ErrorDescriber = errors;
            Logger = logger;

            if (userValidators != null)
            {
                foreach (var v in userValidators)
                {
                    UserValidators.Add(v);
                }
            }
            if (passwordValidators != null)
            {
                foreach (var v in passwordValidators)
                {
                    PasswordValidators.Add(v);
                }
            }

            if (tokenProviders != null)
            {
                foreach (var tokenProvider in tokenProviders)
                {
                    RegisterTokenProvider(tokenProvider);
                }
            }
        }

        /// <summary>
        /// Gets or sets the persistence store the manager operates over.
        /// </summary>
        /// <value>The persistence store the manager operates over.</value>
        protected internal IUserStore<TUser> Store { get; set; }

        /// <summary>
        /// Gets the <see cref="ILogger"/> used to log messages from the manager.
        /// </summary>
        /// <value>
        /// The <see cref="ILogger"/> used to log messages from the manager.
        /// </value>
        protected internal virtual ILogger Logger { get; set; }

        internal IPasswordHasher<TUser> PasswordHasher { get; set; }

        internal IList<IUserValidator<TUser>> UserValidators { get; } = new List<IUserValidator<TUser>>();

        internal IList<IPasswordValidator<TUser>> PasswordValidators { get; } = new List<IPasswordValidator<TUser>>();

        internal ILookupNormalizer KeyNormalizer { get; set; }

        internal IdentityErrorDescriber ErrorDescriber { get; set; }

        internal IdentityOptions Options { get; set; }

        /// <summary>
        /// Gets a flag indicating whether the backing user store supports two factor authentication.
        /// </summary>
        /// <value>
        /// true if the backing user store supports user two factor authentication, otherwise false.
        /// </value>
        public virtual bool SupportsUserTwoFactor
        {
            get
            {
                ThrowIfDisposed();
                return Store is IUserTwoFactorStore<TUser>;
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the backing user store supports user passwords.
        /// </summary>
        /// <value>
        /// true if the backing user store supports user passwords, otherwise false.
        /// </value>
        public virtual bool SupportsUserPassword
        {
            get
            {
                ThrowIfDisposed();
                return Store is IUserPasswordStore<TUser>;
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the backing user store supports security stamps.
        /// </summary>
        /// <value>
        /// true if the backing user store supports user security stamps, otherwise false.
        /// </value>
        public virtual bool SupportsUserSecurityStamp
        {
            get
            {
                ThrowIfDisposed();
                return Store is IUserSecurityStampStore<TUser>;
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the backing user store supports user roles.
        /// </summary>
        /// <value>
        /// true if the backing user store supports user roles, otherwise false.
        /// </value>
        public virtual bool SupportsUserRole
        {
            get
            {
                ThrowIfDisposed();
                return Store is IUserRoleStore<TUser>;
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the backing user store supports external logins.
        /// </summary>
        /// <value>
        /// true if the backing user store supports external logins, otherwise false.
        /// </value>
        public virtual bool SupportsUserLogin
        {
            get
            {
                ThrowIfDisposed();
                return Store is IUserLoginStore<TUser>;
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the backing user store supports user emails.
        /// </summary>
        /// <value>
        /// true if the backing user store supports user emails, otherwise false.
        /// </value>
        public virtual bool SupportsUserEmail
        {
            get
            {
                ThrowIfDisposed();
                return Store is IUserEmailStore<TUser>;
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the backing user store supports user telephone numbers.
        /// </summary>
        /// <value>
        /// true if the backing user store supports user telephone numbers, otherwise false.
        /// </value>
        public virtual bool SupportsUserPhoneNumber
        {
            get
            {
                ThrowIfDisposed();
                return Store is IUserPhoneNumberStore<TUser>;
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the backing user store supports user claims.
        /// </summary>
        /// <value>
        /// true if the backing user store supports user claims, otherwise false.
        /// </value>
        public virtual bool SupportsUserClaim
        {
            get
            {
                ThrowIfDisposed();
                return Store is IUserClaimStore<TUser>;
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the backing user store supports user lock-outs.
        /// </summary>
        /// <value>
        /// true if the backing user store supports user lock-outs, otherwise false.
        /// </value>
        public virtual bool SupportsUserLockout
        {
            get
            {
                ThrowIfDisposed();
                return Store is IUserLockoutStore<TUser>;
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the backing user store supports returning 
        /// <see cref="IQueryable"/> collections of information.
        /// </summary>
        /// <value>
        /// true if the backing user store supports returning <see cref="IQueryable"/> collections of 
        /// information, otherwise false.
        /// </value>
        public virtual bool SupportsQueryableUsers
        {
            get
            {
                ThrowIfDisposed();
                return Store is IQueryableUserStore<TUser>;
            }
        }

        /// <summary>
        ///     Returns an IQueryable of users if the store is an IQueryableUserStore
        /// </summary>
        public virtual IQueryable<TUser> Users
        {
            get
            {
                var queryableStore = Store as IQueryableUserStore<TUser>;
                if (queryableStore == null)
                {
                    throw new NotSupportedException(Resources.StoreNotIQueryableUserStore);
                }
                return queryableStore.Users;
            }
        }

        /// <summary>
        /// Releases all resources used by the user manager.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Generates a value suitable for use in concurrency tracking, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to generate the stamp for.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the security
        /// stamp for the specified <paramref name="user"/>.
        /// </returns>
        public virtual Task<string> GenerateConcurrencyStampAsync(TUser user)
        {
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Creates the specified <paramref name="user"/> in the backing store with no password, 
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to create.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> CreateAsync(TUser user)
        {
            ThrowIfDisposed();
            await UpdateSecurityStampInternal(user);
            var result = await ValidateUserInternal(user);
            if (!result.Succeeded)
            {
                return result;
            }
            if (Options.Lockout.EnabledByDefault && SupportsUserLockout)
            {
                await GetUserLockoutStore().SetLockoutEnabledAsync(user, true, CancellationToken);
            }
            await UpdateNormalizedUserNameAsync(user);
            await UpdateNormalizedEmailAsync(user);

            result = await Store.CreateAsync(user, CancellationToken);
            using (await BeginLoggingScopeAsync(user))
            {
                return Logger.Log(result);
            }
        }

        /// <summary>
        /// Updates the specified <paramref name="user"/> in the backing store, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to update.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> UpdateAsync(TUser user)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Deletes the specified <paramref name="user"/> from the backing store, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to delete.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> DeleteAsync(TUser user)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                return Logger.Log(await Store.DeleteAsync(user, CancellationToken));
            }
        }

        /// <summary>
        /// Finds and returns a user, if any, who has the specified <paramref name="userId"/>.
        /// </summary>
        /// <param name="userId">The user ID to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the user matching the specified <paramref name="userID"/> if it exists.
        /// </returns>
        public virtual Task<TUser> FindByIdAsync(string userId)
        {
            ThrowIfDisposed();
            return Store.FindByIdAsync(userId, CancellationToken);
        }

        /// <summary>
        /// Finds and returns a user, if any, who has the specified normalized user name.
        /// </summary>
        /// <param name="normalizedUserName">The normalized user name to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the user matching the specified <paramref name="userID"/> if it exists.
        /// </returns>
        public virtual Task<TUser> FindByNameAsync(string userName)
        {
            ThrowIfDisposed();
            if (userName == null)
            {
                throw new ArgumentNullException("userName");
            }
            userName = NormalizeKey(userName);
            return Store.FindByNameAsync(userName, CancellationToken);
        }

        /// <summary>
        /// Creates the specified <paramref name="user"/> in the backing store with given password, 
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to create.</param>
        /// <param name="password">The password for the user to hash and store.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> CreateAsync(TUser user, string password)
        {
            ThrowIfDisposed();
            var passwordStore = GetPasswordStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (password == null)
            {
                throw new ArgumentNullException("password");
            }
            var result = await UpdatePasswordHash(passwordStore, user, password);
            if (!result.Succeeded)
            {
                return result;
            }
            return await CreateAsync(user);
        }

        /// <summary>
        /// Normalize a key (user name, email) for consistent comparisons.
        /// </summary>
        /// <param name="key">The key to normalize.</param>
        /// <returns>A normalized value representing the specified <paramref name="key"/>.</returns>
        public virtual string NormalizeKey(string key)
        {
            return (KeyNormalizer == null) ? key : KeyNormalizer.Normalize(key);
        }

        /// <summary>
        /// Updates the normalized user name for the specified <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose user name should be normalized and updated.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task UpdateNormalizedUserNameAsync(TUser user)
        {
            var normalizedName = NormalizeKey(await GetUserNameAsync(user));
            await Store.SetNormalizedUserNameAsync(user, normalizedName, CancellationToken);
        }

        /// <summary>
        /// Gets the user name for the specified <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose name should be retrieved.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the name for the specified <paramref name="user"/>.</returns>
        public virtual async Task<string> GetUserNameAsync(TUser user)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await Store.GetUserNameAsync(user, CancellationToken);
        }

        /// <summary>
        /// Sets the given <paramref name="userName" /> for the specified <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose name should be set.</param>
        /// <param name="userName">The user name to set.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task<IdentityResult> SetUserNameAsync(TUser user, string userName)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                await Store.SetUserNameAsync(user, userName, CancellationToken);
                await UpdateSecurityStampInternal(user);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Gets the user identifier for the specified <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose identifier should be retrieved.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the identifier for the specified <paramref name="user"/>.</returns>
        public virtual async Task<string> GetUserIdAsync(TUser user)
        {
            ThrowIfDisposed();
            return await Store.GetUserIdAsync(user, CancellationToken);
        }

        /// <summary>
        /// Returns a flag indicating whether the given <paramref name="password"/> is valid for the 
        /// specified <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose password should be validated.</param>
        /// <param name="password">The password to validate</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing true if
        /// the specified <paramref name="password" /> matches the one store for the <paramref name="user"/>,
        /// otherwise false.</returns>
        public virtual async Task<bool> CheckPasswordAsync(TUser user, string password)
        {
            ThrowIfDisposed();
            var passwordStore = GetPasswordStore();
            if (user == null)
            {
                return false;
            }

            using (await BeginLoggingScopeAsync(user))
            {
                var result = await VerifyPasswordAsync(passwordStore, user, password);
                if (result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    Logger.Log(await UpdatePasswordHash(passwordStore, user, password, validatePassword: false));
                    Logger.Log(await UpdateUserAsync(user));
                }

                return Logger.Log(result != PasswordVerificationResult.Failed);
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the specified <paramref name="user"/> has a password, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to return a flag for, indicating whether they have a password or not.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, returning true if the specified <paramref name="user"/> has a password
        /// otherwise false.
        /// </returns>
        public virtual async Task<bool> HasPasswordAsync(TUser user)
        {
            ThrowIfDisposed();
            var passwordStore = GetPasswordStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                return Logger.Log(await passwordStore.HasPasswordAsync(user, CancellationToken));
            }
        }

        /// <summary>
        /// Adds the <paramref name="password"/> to the specified <paramref name="user"/> only if the user
        /// does not already have a password, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose password should be set.</param>
        /// <param name="password">The password to set.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> AddPasswordAsync(TUser user, string password)
        {
            ThrowIfDisposed();
            var passwordStore = GetPasswordStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                var hash = await passwordStore.GetPasswordHashAsync(user, CancellationToken);
                if (hash != null)
                {
                    return Logger.Log(IdentityResult.Failed(ErrorDescriber.UserAlreadyHasPassword()));
                }
                var result = await UpdatePasswordHash(passwordStore, user, password);
                if (!result.Succeeded)
                {
                    return Logger.Log(result);
                }
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Changes a user's password after confirming the specified <paramref name="currentPassword"/> is correct,
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose password should be set.</param>
        /// <param name="currentPassword">The current password to validate before changing.</param>
        /// <param name="newPassword">The new password to set for the specified <paramref name="user"/>.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> ChangePasswordAsync(TUser user, string currentPassword, string newPassword)
        {
            ThrowIfDisposed();
            var passwordStore = GetPasswordStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                if (await VerifyPasswordAsync(passwordStore, user, currentPassword) != PasswordVerificationResult.Failed)
                {
                    var result = await UpdatePasswordHash(passwordStore, user, newPassword);
                    if (!result.Succeeded)
                    {
                        return Logger.Log(result);
                    }
                    return Logger.Log(await UpdateUserAsync(user));
                }
                return Logger.Log(IdentityResult.Failed(ErrorDescriber.PasswordMismatch()));
            }
        }

        /// <summary>
        /// Removes a user's password, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose password should be removed.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> RemovePasswordAsync(TUser user,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            var passwordStore = GetPasswordStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                await UpdatePasswordHash(passwordStore, user, null, validatePassword: false);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Returns a <see cref="PasswordVerificationResult"/> indicating the result of a password hash comparison.
        /// </summary>
        /// <param name="store">The store containing a user's password.</param>
        /// <param name="user">The user whose password should be verified.</param>
        /// <param name="password">The password to verify.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="PasswordVerificationResult"/>
        /// of the operation.
        /// </returns>
        protected virtual async Task<PasswordVerificationResult> VerifyPasswordAsync(IUserPasswordStore<TUser> store, TUser user, string password)
        {
            var hash = await store.GetPasswordHashAsync(user, CancellationToken);
            if (hash == null)
            {
                return PasswordVerificationResult.Failed;
            }
            return PasswordHasher.VerifyHashedPassword(user, hash, password);
        }

        /// <summary>
        /// Get the security stamp for the specified <paramref name="user" />, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose security stamp should be set.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the security stamp for the specified <paramref name="user"/>.</returns>
        public virtual async Task<string> GetSecurityStampAsync(TUser user)
        {
            ThrowIfDisposed();
            var securityStore = GetSecurityStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await securityStore.GetSecurityStampAsync(user, CancellationToken);
        }

        /// <summary>
        /// Regenerates the security stamp for the specified <paramref name="user" />, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose security stamp should be regenerated.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        /// <remarks>
        /// Regenerating a security stamp will sign out any saved login for the user.
        /// </remarks>
        public virtual async Task<IdentityResult> UpdateSecurityStampAsync(TUser user)
        {
            ThrowIfDisposed();
            GetSecurityStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                await UpdateSecurityStampInternal(user);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Generates a password reset token for the specified <paramref name="user"/>, using
        /// the configured password reset token provider, as an asynchronous operation. 
        /// </summary>
        /// <param name="user">The user to generate a password reset token for.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, 
        /// containing a password reset token for the specified <paramref name="user"/>.</returns>
        public virtual async Task<string> GeneratePasswordResetTokenAsync(TUser user)
        {
            ThrowIfDisposed();

            using (await BeginLoggingScopeAsync(user))
            {
                var token = await GenerateUserTokenAsync(user, Options.PasswordResetTokenProvider, "ResetPassword");
                Logger.Log(IdentityResult.Success);
                return token;
            }
        }

        /// <summary>
        /// Resets the <paramref name="user"/>'s password to the specified <paramref name="newPassword"/> after
        /// validating the given password reset <paramref name="token"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose password should be reset.</param>
        /// <param name="token">The password reset token to verify.</param>
        /// <param name="newPassword">The new password to set if reset token verification fails.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> ResetPasswordAsync(TUser user, string token, string newPassword)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                // Make sure the token is valid and the stamp matches
                if (!await VerifyUserTokenAsync(user, Options.PasswordResetTokenProvider, "ResetPassword", token))
                {
                    return Logger.Log(IdentityResult.Failed(ErrorDescriber.InvalidToken()));
                }
                var passwordStore = GetPasswordStore();
                var result = await UpdatePasswordHash(passwordStore, user, newPassword);
                if (!result.Succeeded)
                {
                    return Logger.Log(result);
                }
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Retrieves the user associated with the specified external login provider and login provider key, as an asynchronous operation..
        /// </summary>
        /// <param name="loginProvider">The login provider who provided the <paramref name="providerKey"/>.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <returns>
        /// The <see cref="Task"/> for the asynchronous operation, containing the user, if any which matched the specified login provider and key.
        /// </returns>
        public virtual Task<TUser> FindByLoginAsync(string loginProvider, string providerKey)
        {
            ThrowIfDisposed();
            var loginStore = GetLoginStore();
            if (loginProvider == null)
            {
                throw new ArgumentNullException("loginProvider");
            }
            if (providerKey == null)
            {
                throw new ArgumentNullException("providerKey");
            }
            return loginStore.FindByLoginAsync(loginProvider, providerKey, CancellationToken);
        }

        /// <summary>
        /// Attempts to remove the provided external login information from the specified <paramref name="user"/>, as an asynchronous operation.
        /// and returns a flag indicating whether the removal succeed or not.
        /// </summary>
        /// <param name="user">The user to remove the login information from.</param>
        /// <param name="loginProvider">The login provide whose information should be removed.</param>
        /// <param name="providerKey">The key given by the external login provider for the specified user.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> RemoveLoginAsync(TUser user, string loginProvider, string providerKey)
        {
            ThrowIfDisposed();
            var loginStore = GetLoginStore();
            if (loginProvider == null)
            {
                throw new ArgumentNullException("loginProvider");
            }
            if (providerKey == null)
            {
                throw new ArgumentNullException("providerKey");
            }
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                await loginStore.RemoveLoginAsync(user, loginProvider, providerKey, CancellationToken);
                await UpdateSecurityStampInternal(user);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Adds an external <see cref="UserLoginInfo"/> to the specified <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to add the login to.</param>
        /// <param name="login">The external <see cref="UserLoginInfo"/> to add to the specified <paramref name="user"/>.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> AddLoginAsync(TUser user, UserLoginInfo login)
        {
            ThrowIfDisposed();
            var loginStore = GetLoginStore();
            if (login == null)
            {
                throw new ArgumentNullException("login");
            }
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                var existingUser = await FindByLoginAsync(login.LoginProvider, login.ProviderKey);
                if (existingUser != null)
                {
                    return Logger.Log(IdentityResult.Failed(ErrorDescriber.LoginAlreadyAssociated()));
                }
                await loginStore.AddLoginAsync(user, login, CancellationToken);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Retrieves the associated logins for the specified <param ref="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose associated logins to retrieve.</param>
        /// <returns>
        /// The <see cref="Task"/> for the asynchronous operation, containing a list of <see cref="UserLoginInfo"/> for the specified <paramref name="user"/>, if any.
        /// </returns>
        public virtual async Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user)
        {
            ThrowIfDisposed();
            var loginStore = GetLoginStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await loginStore.GetLoginsAsync(user, CancellationToken);
        }

        /// <summary>
        /// Adds the specified <paramref name="claim"/> to the <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to add the claim to.</param>
        /// <param name="claim">The claim to add.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual Task<IdentityResult> AddClaimAsync(TUser user, Claim claim)
        {
            ThrowIfDisposed();
            var claimStore = GetClaimStore();
            if (claim == null)
            {
                throw new ArgumentNullException("claim");
            }
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return AddClaimsAsync(user, new Claim[] { claim });
        }

        /// <summary>
        /// Adds the specified <paramref name="claims"/> to the <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to add the claim to.</param>
        /// <param name="claims">The claims to add.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> AddClaimsAsync(TUser user, IEnumerable<Claim> claims)
        {
            ThrowIfDisposed();
            var claimStore = GetClaimStore();
            if (claims == null)
            {
                throw new ArgumentNullException("claims");
            }
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                await claimStore.AddClaimsAsync(user, claims, CancellationToken);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Replaces the given <paramref name="claim"/> on the specified <paramref name="user"/> with the <paramref name="newClaim"/>
        /// </summary>
        /// <param name="user">The user to replace the claim on.</param>
        /// <param name="claim">The claim to replace.</param>
        /// <param name="newClaim">The new claim to replace the existing <paramref name="claim"/> with.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim)
        {
            ThrowIfDisposed();
            var claimStore = GetClaimStore();
            if (claim == null)
            {
                throw new ArgumentNullException("claim");
            }
            if (newClaim == null)
            {
                throw new ArgumentNullException("newClaim");
            }
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                await claimStore.ReplaceClaimAsync(user, claim, newClaim, CancellationToken);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Removes the specified <paramref name="claim"/> from the given <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the specified <paramref name="claims"/> from.</param>
        /// <param name="claim">The <see cref="Claim"/> to remove.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual Task<IdentityResult> RemoveClaimAsync(TUser user, Claim claim)
        {
            ThrowIfDisposed();
            var claimStore = GetClaimStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (claim == null)
            {
                throw new ArgumentNullException("claim");
            }
            return RemoveClaimsAsync(user, new Claim[] { claim });
        }

        /// <summary>
        /// Removes the specified <paramref name="claims"/> from the given <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the specified <paramref name="claims"/> from.</param>
        /// <param name="claims">A collection of <see cref="Claim"/>s to remove.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims)
        {
            ThrowIfDisposed();
            var claimStore = GetClaimStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (claims == null)
            {
                throw new ArgumentNullException("claims");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                await claimStore.RemoveClaimsAsync(user, claims, CancellationToken);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Gets a list of <see cref="Claim"/>s to be belonging to the specified <paramref name="user"/> as an asynchronous operation.
        /// </summary>
        /// <param name="user">The role whose claims to retrieve.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> that represents the result of the asynchronous query, a list of <see cref="Claim"/>s.
        /// </returns>
        public virtual async Task<IList<Claim>> GetClaimsAsync(TUser user)
        {
            ThrowIfDisposed();
            var claimStore = GetClaimStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await claimStore.GetClaimsAsync(user, CancellationToken);
        }

        /// <summary>
        /// Add the specified <paramref name="user"/> to the named role, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to add to the named role.</param>
        /// <param name="roleName">The name of the role to add the user to.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> AddToRoleAsync(TUser user, string role)
        {
            ThrowIfDisposed();
            var userRoleStore = GetUserRoleStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                var userRoles = await userRoleStore.GetRolesAsync(user, CancellationToken);
                if (userRoles.Contains(role))
                {
                    return Logger.Log(IdentityResult.Failed(ErrorDescriber.UserAlreadyInRole(role)));
                }
                await userRoleStore.AddToRoleAsync(user, role, CancellationToken);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Add the specified <paramref name="user"/> to the named roles, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to add to the named roles.</param>
        /// <param name="roleName">The name of the roles to add the user to.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> AddToRolesAsync(TUser user, IEnumerable<string> roles)
        {
            ThrowIfDisposed();
            var userRoleStore = GetUserRoleStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (roles == null)
            {
                throw new ArgumentNullException("roles");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                var userRoles = await userRoleStore.GetRolesAsync(user, CancellationToken);
                foreach (var role in roles)
                {
                    if (userRoles.Contains(role))
                    {
                        return Logger.Log(IdentityResult.Failed(ErrorDescriber.UserAlreadyInRole(role)));
                    }
                    await userRoleStore.AddToRoleAsync(user, role, CancellationToken);
                }
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Removes the specified <paramref name="user"/> from the named role, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to remove from the named role.</param>
        /// <param name="roleName">The name of the role to remove the user from.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> RemoveFromRoleAsync(TUser user, string role)
        {
            ThrowIfDisposed();
            var userRoleStore = GetUserRoleStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                if (!await userRoleStore.IsInRoleAsync(user, role, CancellationToken))
                {
                    return Logger.Log(IdentityResult.Failed(ErrorDescriber.UserNotInRole(role)));
                }
                await userRoleStore.RemoveFromRoleAsync(user, role, CancellationToken);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Removes the specified <paramref name="user"/> from the named roles, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to remove from the named roles.</param>
        /// <param name="roleName">The name of the roles to remove the user from.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> RemoveFromRolesAsync(TUser user, IEnumerable<string> roles)
        {
            ThrowIfDisposed();
            var userRoleStore = GetUserRoleStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (roles == null)
            {
                throw new ArgumentNullException("roles");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                foreach (var role in roles)
                {
                    if (!await userRoleStore.IsInRoleAsync(user, role, CancellationToken))
                    {
                        return Logger.Log(IdentityResult.Failed(ErrorDescriber.UserNotInRole(role)));
                    }
                    await userRoleStore.RemoveFromRoleAsync(user, role, CancellationToken);
                }
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Gets a list of role names the specified <paramref name="user"/> belongs to, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose role names to retrieve.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing a list of role names.</returns>
        public virtual async Task<IList<string>> GetRolesAsync(TUser user)
        {
            ThrowIfDisposed();
            var userRoleStore = GetUserRoleStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await userRoleStore.GetRolesAsync(user, CancellationToken);
        }

        /// <summary>
        /// Returns a flag indicating whether the specified <paramref name="user"/> is a member of the give named role, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose role membership should be checked.</param>
        /// <param name="role">The name of the role to be checked.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing a flag indicating whether the specified <see cref="user"/> is
        /// a member of the named role.
        /// </returns>
        public virtual async Task<bool> IsInRoleAsync(TUser user, string role)
        {
            ThrowIfDisposed();
            var userRoleStore = GetUserRoleStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await userRoleStore.IsInRoleAsync(user, role, CancellationToken);
        }
        
        /// <summary>
        /// Gets the email address for the specified <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose email should be returned.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The task object containing the results of the asynchronous operation, the email address for the specified <paramref name="user"/>.</returns>
        public virtual async Task<string> GetEmailAsync(TUser user)
        {
            ThrowIfDisposed();
            var store = GetEmailStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await store.GetEmailAsync(user, CancellationToken);
        }

        /// <summary>
        /// Sets the <paramref name="email"/> address for a <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose email should be set.</param>
        /// <param name="email">The email to set.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> SetEmailAsync(TUser user, string email)
        {
            ThrowIfDisposed();
            var store = GetEmailStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                await store.SetEmailAsync(user, email, CancellationToken);
                await store.SetEmailConfirmedAsync(user, false, CancellationToken);
                await UpdateSecurityStampInternal(user);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Gets the user, if any, associated with the specified, normalized email address.
        /// </summary>
        /// <param name="normalizedEmail">The normalized email address to return the user for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The task object containing the results of the asynchronous lookup operation, the user if any associated with the specified normalized email address.
        /// </returns>
        public virtual Task<TUser> FindByEmailAsync(string email)
        {
            ThrowIfDisposed();
            var store = GetEmailStore();
            if (email == null)
            {
                throw new ArgumentNullException("email");
            }
            return store.FindByEmailAsync(NormalizeKey(email), CancellationToken);
        }

        /// <summary>
        /// Updates the normalized email for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose email address should be normalized and updated.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public virtual async Task UpdateNormalizedEmailAsync(TUser user)
        {
            var store = GetEmailStore(throwOnFail: false);
            if (store != null)
            {
                var email = await GetEmailAsync(user);
                await store.SetNormalizedEmailAsync(user, NormalizeKey(email), CancellationToken);
            }
        }

        /// <summary>
        /// Generates an email confirmation token for the specified user, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to generate an email confirmation token for.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, an email confirmation token.
        /// </returns>
        public async virtual Task<string> GenerateEmailConfirmationTokenAsync(TUser user)
        {
            ThrowIfDisposed();

            using (await BeginLoggingScopeAsync(user))
            {
                var token = await GenerateUserTokenAsync(user, Options.EmailConfirmationTokenProvider, "Confirmation");
                Logger.Log(IdentityResult.Success);
                return token;
            }
        }

        /// <summary>
        /// Validates that an email confirmation token matches the specified <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to validate the token against.</param>
        /// <param name="token">The email confirmation token to validate.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> ConfirmEmailAsync(TUser user, string token)
        {
            ThrowIfDisposed();
            var store = GetEmailStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                if (!await VerifyUserTokenAsync(user, Options.EmailConfirmationTokenProvider, "Confirmation", token))
                {
                    return Logger.Log(IdentityResult.Failed(ErrorDescriber.InvalidToken()));
                }
                await store.SetEmailConfirmedAsync(user, true, CancellationToken);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the email address for the specified <paramref name="user"/> has been verified, true if the email address is verified otherwise
        /// false, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose email confirmation status should be returned.</param>
        /// <returns>
        /// The task object containing the results of the asynchronous operation, a flag indicating whether the email address for the specified <paramref name="user"/>
        /// has been confirmed or not.
        /// </returns>
        public virtual async Task<bool> IsEmailConfirmedAsync(TUser user)
        {
            ThrowIfDisposed();
            var store = GetEmailStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await store.GetEmailConfirmedAsync(user, CancellationToken);
        }

        /// <summary>
        /// Generates an email change token for the specified user, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to generate an email change token for.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, an email change token.
        /// </returns>
        public virtual async Task<string> GenerateChangeEmailTokenAsync(TUser user, string newEmail)
        {
            ThrowIfDisposed();

            using (await BeginLoggingScopeAsync(user))
            {
                var token = await GenerateUserTokenAsync(user, Options.ChangeEmailTokenProvider, GetChangeEmailPurpose(newEmail));
                Logger.Log(IdentityResult.Success);
                return token;
            }
        }

        /// <summary>
        /// Updates a users emails if the specified email change <paramref name="token"/> is valid for the user.
        /// </summary>
        /// <param name="user">The user whose email should be updated.</param>
        /// <param name="newEmail">The new email address.</param>
        /// <param name="token">The change email token to be verified.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> ChangeEmailAsync(TUser user, string newEmail, string token)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                // Make sure the token is valid and the stamp matches
                if (!await VerifyUserTokenAsync(user, Options.ChangeEmailTokenProvider, GetChangeEmailPurpose(newEmail), token))
                {
                    return Logger.Log(IdentityResult.Failed(ErrorDescriber.InvalidToken()));
                }
                var store = GetEmailStore();
                await store.SetEmailAsync(user, newEmail, CancellationToken);
                await store.SetEmailConfirmedAsync(user, true, CancellationToken);
                await UpdateSecurityStampInternal(user);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Gets the telephone number, if any, for the specified <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose telephone number should be retrieved.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the user's telephone number, if any.</returns>
        public virtual async Task<string> GetPhoneNumberAsync(TUser user)
        {
            ThrowIfDisposed();
            var store = GetPhoneNumberStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await store.GetPhoneNumberAsync(user, CancellationToken);
        }

        /// <summary>
        /// Sets the phone number for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose phone number to set.</param>
        /// <param name="phoneNumber">The phone number to set.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> SetPhoneNumberAsync(TUser user, string phoneNumber)
        {
            ThrowIfDisposed();
            var store = GetPhoneNumberStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                await store.SetPhoneNumberAsync(user, phoneNumber, CancellationToken);
                await store.SetPhoneNumberConfirmedAsync(user, false, CancellationToken);
                await UpdateSecurityStampInternal(user);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Sets the phone number for the specified <paramref name="user"/> if the specified 
        /// change <paramref name="token"/> is valid.
        /// </summary>
        /// <param name="user">The user whose phone number to set.</param>
        /// <param name="phoneNumber">The phone number to set.</param>
        /// <param name="token">The phone number confirmation token to validate.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/>
        /// of the operation.
        /// </returns>
        public virtual async Task<IdentityResult> ChangePhoneNumberAsync(TUser user, string phoneNumber, string token)
        {
            ThrowIfDisposed();
            var store = GetPhoneNumberStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                if (!await VerifyChangePhoneNumberTokenAsync(user, token, phoneNumber))
                {
                    return Logger.Log(IdentityResult.Failed(ErrorDescriber.InvalidToken()));
                }
                await store.SetPhoneNumberAsync(user, phoneNumber, CancellationToken);
                await store.SetPhoneNumberConfirmedAsync(user, true, CancellationToken);
                await UpdateSecurityStampInternal(user);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Gets a flag indicating whether the specified <paramref name="user"/>'s telephone number has been confirmed, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to return a flag for, indicating whether their telephone number is confirmed.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, returning true if the specified <paramref name="user"/> has a confirmed
        /// telephone number otherwise false.
        /// </returns>
        public virtual async Task<bool> IsPhoneNumberConfirmedAsync(TUser user)
        {
            ThrowIfDisposed();
            var store = GetPhoneNumberStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await store.GetPhoneNumberConfirmedAsync(user, CancellationToken);
        }

        /// <summary>
        /// Generates a telephone number change token for the specified user, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to generate a telephone number token for.</param>
        /// <param name="phoneNumber">The new phone number the validation token should be sent to.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the telephone change number token.
        /// </returns>
        public virtual async Task<string> GenerateChangePhoneNumberTokenAsync(TUser user, string phoneNumber)
        {
            ThrowIfDisposed();

            using (await BeginLoggingScopeAsync(user))
            {
                var token = Rfc6238AuthenticationService.GenerateCode(
                        await CreateSecurityTokenAsync(user), phoneNumber)
                           .ToString(CultureInfo.InvariantCulture);
                Logger.Log(IdentityResult.Success);
                return token;
            }
        }

        /// <summary>
        /// Returns a flag indicating whether the specified <paramref name="user"/>'s phone number change verification
        /// token is valid for the given <paramref name="phoneNumber"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to validate the token against.</param>
        /// <param name="token">The telephone number change token to validate.</param>
        /// <param name="phoneNumber">The telephone number the token was generated for.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, returning true if the <paramref name="token"/>
        /// is valid, otherwise false.
        /// </returns>
        public virtual async Task<bool> VerifyChangePhoneNumberTokenAsync(TUser user, string token, string phoneNumber)
        {
            ThrowIfDisposed();

            using (await BeginLoggingScopeAsync(user))
            {
                var securityToken = await CreateSecurityTokenAsync(user);
                int code;
                if (securityToken != null && Int32.TryParse(token, out code))
                {
                    if (Rfc6238AuthenticationService.ValidateCode(securityToken, code, phoneNumber))
                    {
                        Logger.Log(IdentityResult.Success);
                        return true;
                    }
                }
                Logger.Log(IdentityResult.Failed(ErrorDescriber.InvalidToken()));
                return false;
            }
        }

        /// <summary>
        /// Returns a flag indicating whether the specified <paramref name="token"/> is valid for
        /// the given <paramref name="user"/> and <paramref name="purpose"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user to validate the token against.</param>
        /// <param name="tokenProvider">The token provider used to generate the token.</param>
        /// <param name="purpose">The purpose the token should be generated for.</param>
        /// <param name="token">The token to validate</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, returning true if the <paramref name="token"/>
        /// is valid, otherwise false.
        /// </returns>
        public virtual async Task<bool> VerifyUserTokenAsync(TUser user, string tokenProvider, string purpose, string token)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (tokenProvider == null)
            {
                throw new ArgumentNullException(nameof(tokenProvider));
            }

            using (await BeginLoggingScopeAsync(user))
            {
                if (!_tokenProviders.ContainsKey(tokenProvider))
                {
                    throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Resources.NoTokenProvider, tokenProvider));
                }
                // Make sure the token is valid
                var result = await _tokenProviders[tokenProvider].ValidateAsync(purpose, token, this, user);

                if (result)
                {
                    Logger.Log(IdentityResult.Success);
                }
                else
                {
                    Logger.Log(IdentityResult.Failed(ErrorDescriber.InvalidToken()));
                }

                return result;
            }
        }

        /// <summary>
        /// Generates a token for the given <paramref name="user"/> and <paramref name="purpose"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="purpose">The purpose the token will be for.</param>
        /// <param name="user">The user the token will be for.</param>
        /// <param name="tokenProvider">The provider which will generate the token.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents result of the asynchronous operation, a token for
        /// the given user and purpose.
        /// </returns>
        public virtual async Task<string> GenerateUserTokenAsync(TUser user, string tokenProvider, string purpose)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (tokenProvider == null)
            {
                throw new ArgumentNullException(nameof(tokenProvider));
            }
            if (!_tokenProviders.ContainsKey(tokenProvider))
            {
                throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Resources.NoTokenProvider, tokenProvider));
            }

            //TODO: Should we scope here ?
            var token = await _tokenProviders[tokenProvider].GenerateAsync(purpose, this, user);
            Logger.Log(IdentityResult.Success);
            return token;
        }

        /// <summary>
        /// Registers a token provider.
        /// </summary>
        /// <param name="provider">The provider to register.</param>
        public virtual void RegisterTokenProvider(IUserTokenProvider<TUser> provider)
        {
            ThrowIfDisposed();
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }
            _tokenProviders[provider.Name] = provider;
        }

        /// <summary>
        /// Gets a list of valid two factor token providers for the specified <paramref name="user"/>, 
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user the whose two factor authentication providers will be returned.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents result of the asynchronous operation, a list of two
        /// factor authentication providers for the specified user.
        /// </returns>
        public virtual async Task<IList<string>> GetValidTwoFactorProvidersAsync(TUser user)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            var results = new List<string>();
            foreach (var f in _tokenProviders)
            {
                if (await f.Value.CanGenerateTwoFactorTokenAsync(this, user))
                {
                    results.Add(f.Key);
                }
            }
            return results;
        }

        /// <summary>
        /// Verifies the specified two factor authentication <paramref name="token" /> against the <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user the token is supposed to be for.</param>
        /// <param name="tokenProvider">The provider which will verify the token.</param>
        /// <param name="token">The token to verify.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents result of the asynchronous operation, true if the token is valid,
        /// otherwise false.
        /// </returns>
        public virtual async Task<bool> VerifyTwoFactorTokenAsync(TUser user, string tokenProvider, string token)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (!_tokenProviders.ContainsKey(tokenProvider))
            {
                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture,
                    Resources.NoTokenProvider, tokenProvider));
            }

            using (await BeginLoggingScopeAsync(user))
            {
                // Make sure the token is valid
                var result = await _tokenProviders[tokenProvider].ValidateAsync("TwoFactor", token, this, user);
                if (result)
                {
                    Logger.Log(IdentityResult.Success);
                }
                else
                {
                    Logger.Log(IdentityResult.Failed(ErrorDescriber.InvalidToken()));
                }
                return result;
            }
        }

        /// <summary>
        /// Gets a two factor authentication token for the specified <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user the token is for.</param>
        /// <param name="tokenProvider">The provider which will generate the token.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents result of the asynchronous operation, a two factor authentication token
        /// for the user.
        /// </returns>
        public virtual async Task<string> GenerateTwoFactorTokenAsync(TUser user, string tokenProvider)
        {
            ThrowIfDisposed();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (!_tokenProviders.ContainsKey(tokenProvider))
            {
                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture,
                    Resources.NoTokenProvider, tokenProvider));
            }

            using (await BeginLoggingScopeAsync(user))
            {
                var token = await _tokenProviders[tokenProvider].GenerateAsync("TwoFactor", this, user);
                Logger.Log(IdentityResult.Success);
                return token;
            }
        }

        /// <summary>
        /// Returns a flag indicating whether the specified <paramref name="user"/> has two factor authentication enabled or not,
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose two factor authentication enabled status should be retrieved.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, true if the specified <paramref name="user "/> 
        /// has two factor authentication enabled, otherwise false.
        /// </returns>
        public virtual async Task<bool> GetTwoFactorEnabledAsync(TUser user)
        {
            ThrowIfDisposed();
            var store = GetUserTwoFactorStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await store.GetTwoFactorEnabledAsync(user, CancellationToken);
        }

        /// <summary>
        /// Sets a flag indicating whether the specified <paramref name="user"/> has two factor authentication enabled or not,
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose two factor authentication enabled status should be set.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, the <see cref="IdentityResult"/> of the operation
        /// </returns>
        public virtual async Task<IdentityResult> SetTwoFactorEnabledAsync(TUser user, bool enabled)
        {
            ThrowIfDisposed();
            var store = GetUserTwoFactorStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                await store.SetTwoFactorEnabledAsync(user, enabled, CancellationToken);
                await UpdateSecurityStampInternal(user);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Returns a flag indicating whether the specified <paramref name="user"/> his locked out,
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose locked out status should be retrieved.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, true if the specified <paramref name="user "/> 
        /// is locked out, otherwise false.
        /// </returns>
        public virtual async Task<bool> IsLockedOutAsync(TUser user)
        {
            ThrowIfDisposed();
            var store = GetUserLockoutStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (!await store.GetLockoutEnabledAsync(user, CancellationToken))
            {
                return false;
            }
            var lockoutTime = await store.GetLockoutEndDateAsync(user, CancellationToken);
            return lockoutTime >= DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Sets a flag indicating whether the specified <paramref name="user"/> is locked out,
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose locked out status should be set.</param>
        /// <param name="enabled">Flag indicating whether the user is locked out or not.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, the <see cref="IdentityResult"/> of the operation
        /// </returns>
        public virtual async Task<IdentityResult> SetLockoutEnabledAsync(TUser user, bool enabled)
        {
            ThrowIfDisposed();
            var store = GetUserLockoutStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                await store.SetLockoutEnabledAsync(user, enabled, CancellationToken);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Retrieves a flag indicating whether user lockout can enabled for the specified user, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose ability to be locked out should be returned.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, true if a user can be locked out, otherwise false.
        /// </returns>
        public virtual async Task<bool> GetLockoutEnabledAsync(TUser user)
        {
            ThrowIfDisposed();
            var store = GetUserLockoutStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await store.GetLockoutEnabledAsync(user, CancellationToken);
        }

        /// <summary>
        /// Gets the last <see cref="DateTimeOffset"/> a user's last lockout expired, if any, as an asynchronous operation.
        /// Any time in the past should be indicates a user is not locked out.
        /// </summary>
        /// <param name="user">The user whose lockout date should be retrieved.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> that represents the lookup, a <see cref="DateTimeOffset"/> containing the last time a user's lockout expired, if any.
        /// </returns>
        public virtual async Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user)
        {
            ThrowIfDisposed();
            var store = GetUserLockoutStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await store.GetLockoutEndDateAsync(user, CancellationToken);
        }

        /// <summary>
        /// Locks out a user until the specified end date has passed, as an asynchronous operation. Setting a end date in the past immediately unlocks a user.
        /// </summary>
        /// <param name="user">The user whose lockout date should be set.</param>
        /// <param name="lockoutEnd">The <see cref="DateTimeOffset"/> after which the <paramref name="user"/>'s lockout should end.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the operation.</returns>
        public virtual async Task<IdentityResult> SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd)
        {
            ThrowIfDisposed();
            var store = GetUserLockoutStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                if (!await store.GetLockoutEnabledAsync(user, CancellationToken))
                {
                    return Logger.Log(IdentityResult.Failed(ErrorDescriber.UserLockoutNotEnabled()));
                }
                await store.SetLockoutEndDateAsync(user, lockoutEnd, CancellationToken);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Increments the access failed count for the user as an asynchronous operation. 
        /// If the failed access account is greater than or equal to the configured maximum number of attempts, 
        /// the user will be locked out for the configured lockout time span.
        /// </summary>
        /// <param name="user">The user whose failed access count to increment.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the operation.</returns>
        public virtual async Task<IdentityResult> AccessFailedAsync(TUser user)
        {
            ThrowIfDisposed();
            var store = GetUserLockoutStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                // If this puts the user over the threshold for lockout, lock them out and reset the access failed count
                var count = await store.IncrementAccessFailedCountAsync(user, CancellationToken);
                if (count < Options.Lockout.MaxFailedAccessAttempts)
                {
                    return Logger.Log(await UpdateUserAsync(user));
                }
                await store.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.Add(Options.Lockout.DefaultLockoutTimeSpan),
                    CancellationToken);
                await store.ResetAccessFailedCountAsync(user, CancellationToken);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Resets the access failed count for the specified <paramref name="user"/>, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose failed access count should be reset.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the operation.</returns>
        public virtual async Task<IdentityResult> ResetAccessFailedCountAsync(TUser user)
        {
            ThrowIfDisposed();
            var store = GetUserLockoutStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            using (await BeginLoggingScopeAsync(user))
            {
                if (await GetAccessFailedCountAsync(user) == 0)
                {
                    return Logger.Log(IdentityResult.Success);
                }
                await store.ResetAccessFailedCountAsync(user, CancellationToken);
                return Logger.Log(await UpdateUserAsync(user));
            }
        }

        /// <summary>
        /// Retrieves the current number of failed accesses for the given <paramref name="user"/>, as an asynchronous operation. 
        /// </summary>
        /// <param name="user">The user whose access failed count should be retrieved for.</param>
        /// <returns>The <see cref="Task"/> that contains the result the asynchronous operation, the current failed access count
        /// for the user..</returns>
        public virtual async Task<int> GetAccessFailedCountAsync(TUser user)
        {
            ThrowIfDisposed();
            var store = GetUserLockoutStore();
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            return await store.GetAccessFailedCountAsync(user, CancellationToken);
        }

        public virtual Task<IList<TUser>> GetUsersForClaimAsync(Claim claim)
        {
            ThrowIfDisposed();
            var store = GetClaimStore();
            if (claim == null)
            {
                throw new ArgumentNullException("claim");
            }
            return store.GetUsersForClaimAsync(claim, CancellationToken);
        }

        /// <summary>
        /// Returns a list of users from the user store who have the specified <see cref="Claim"/>.
        /// </summary>
        /// <param name="claim">The claim to look for.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> that represents the result of the asynchronous query, a list of <typeparamref name="TUser"/>s who
        /// have the specified claim.
        /// </returns>
        public virtual Task<IList<TUser>> GetUsersInRoleAsync(string roleName)
        {
            ThrowIfDisposed();
            var store = GetUserRoleStore();
            if (roleName == null)
            {
                throw new ArgumentNullException("role");
            }

            return store.GetUsersInRoleAsync(roleName, CancellationToken);
        }

        /// <summary>
        /// Starts a logging scope to contain the logging messages for an operation, as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user the operation is acting on.</param>
        /// <param name="methodName">The method that called this method.</param>
        /// <returns>A <see cref="Task"/> containing the logging scope.</returns>
        protected virtual async Task<IDisposable> BeginLoggingScopeAsync(TUser user, [CallerMemberName] string methodName = null)
        {
            var state = Resources.FormatLoggingResultMessageForUser(methodName, await GetUserIdAsync(user));
            return Logger?.BeginScope(state);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the role manager and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                Store.Dispose();
                _disposed = true;
            }
        }

        // IUserFactorStore methods
        internal IUserTwoFactorStore<TUser> GetUserTwoFactorStore()
        {
            var cast = Store as IUserTwoFactorStore<TUser>;
            if (cast == null)
            {
                throw new NotSupportedException(Resources.StoreNotIUserTwoFactorStore);
            }
            return cast;
        }

        // IUserLockoutStore methods
        internal IUserLockoutStore<TUser> GetUserLockoutStore()
        {
            var cast = Store as IUserLockoutStore<TUser>;
            if (cast == null)
            {
                throw new NotSupportedException(Resources.StoreNotIUserLockoutStore);
            }
            return cast;
        }

        // IUserEmailStore methods
        internal IUserEmailStore<TUser> GetEmailStore(bool throwOnFail = true)
        {
            var cast = Store as IUserEmailStore<TUser>;
            if (throwOnFail && cast == null)
            {
                throw new NotSupportedException(Resources.StoreNotIUserEmailStore);
            }
            return cast;
        }

        // IUserPhoneNumberStore methods
        internal IUserPhoneNumberStore<TUser> GetPhoneNumberStore()
        {
            var cast = Store as IUserPhoneNumberStore<TUser>;
            if (cast == null)
            {
                throw new NotSupportedException(Resources.StoreNotIUserPhoneNumberStore);
            }
            return cast;
        }

        // Two factor APIS
        internal async Task<byte[]> CreateSecurityTokenAsync(TUser user)
        {
            return Encoding.Unicode.GetBytes(await GetSecurityStampAsync(user));
        }

        // Update the security stamp if the store supports it
        internal async Task UpdateSecurityStampInternal(TUser user)
        {
            if (SupportsUserSecurityStamp)
            {
                await GetSecurityStore().SetSecurityStampAsync(user, NewSecurityStamp(), CancellationToken);
            }
        }

        internal async Task<IdentityResult> UpdatePasswordHash(IUserPasswordStore<TUser> passwordStore,
            TUser user, string newPassword, bool validatePassword = true)
        {
            if (validatePassword)
            {
                var validate = await ValidatePasswordInternal(user, newPassword);
                if (!validate.Succeeded)
                {
                    return validate;
                }
            }
            var hash = newPassword != null ? PasswordHasher.HashPassword(user, newPassword) : null;
            await passwordStore.SetPasswordHashAsync(user, hash, CancellationToken);
            await UpdateSecurityStampInternal(user);
            return IdentityResult.Success;
        }

        private IUserRoleStore<TUser> GetUserRoleStore()
        {
            var cast = Store as IUserRoleStore<TUser>;
            if (cast == null)
            {
                throw new NotSupportedException(Resources.StoreNotIUserRoleStore);
            }
            return cast;
        }

        private static string NewSecurityStamp()
        {
            return Guid.NewGuid().ToString();
        }

        // IUserLoginStore methods
        private IUserLoginStore<TUser> GetLoginStore()
        {
            var cast = Store as IUserLoginStore<TUser>;
            if (cast == null)
            {
                throw new NotSupportedException(Resources.StoreNotIUserLoginStore);
            }
            return cast;
        }

        private IUserSecurityStampStore<TUser> GetSecurityStore()
        {
            var cast = Store as IUserSecurityStampStore<TUser>;
            if (cast == null)
            {
                throw new NotSupportedException(Resources.StoreNotIUserSecurityStampStore);
            }
            return cast;
        }

        private IUserClaimStore<TUser> GetClaimStore()
        {
            var cast = Store as IUserClaimStore<TUser>;
            if (cast == null)
            {
                throw new NotSupportedException(Resources.StoreNotIUserClaimStore);
            }
            return cast;
        }


        private static string GetChangeEmailPurpose(string newEmail)
        {
            return "ChangeEmail:" + newEmail;
        }

        private async Task<IdentityResult> ValidateUserInternal(TUser user)
        {
            var errors = new List<IdentityError>();
            foreach (var v in UserValidators)
            {
                var result = await v.ValidateAsync(this, user);
                if (!result.Succeeded)
                {
                    errors.AddRange(result.Errors);
                }
            }
            return errors.Count > 0 ? IdentityResult.Failed(errors.ToArray()) : IdentityResult.Success;
        }

        private async Task<IdentityResult> ValidatePasswordInternal(TUser user, string password)
        {
            var errors = new List<IdentityError>();
            foreach (var v in PasswordValidators)
            {
                var result = await v.ValidateAsync(this, user, password);
                if (!result.Succeeded)
                {
                    errors.AddRange(result.Errors);
                }
            }
            return errors.Count > 0 ? IdentityResult.Failed(errors.ToArray()) : IdentityResult.Success;
        }

        /// <summary>
        ///     Validate user and update. Called by other UserManager methods
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        private async Task<IdentityResult> UpdateUserAsync(TUser user)
        {
            var result = await ValidateUserInternal(user);
            if (!result.Succeeded)
            {
                return result;
            }
            await UpdateNormalizedUserNameAsync(user);
            await UpdateNormalizedEmailAsync(user);
            return await Store.UpdateAsync(user, CancellationToken);
        }

        // IUserPasswordStore methods
        private IUserPasswordStore<TUser> GetPasswordStore()
        {
            var cast = Store as IUserPasswordStore<TUser>;
            if (cast == null)
            {
                throw new NotSupportedException(Resources.StoreNotIUserPasswordStore);
            }
            return cast;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

    }
}