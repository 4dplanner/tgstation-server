﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.Client
{
	/// <summary>
	/// For managing <see cref="User"/>s
	/// </summary>
	public interface IUsersClient
	{
		/// <summary>
		/// Read the current user's information and general rights
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the current <see cref="User"/></returns>
		Task<User> Read(CancellationToken cancellationToken);

		/// <summary>
		/// Get a specific <paramref name="user"/>
		/// </summary>
		/// <param name="user">The <see cref="User"/> to get</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the requested <paramref name="user"/></returns>
		Task<User> GetId(User user, CancellationToken cancellationToken);

		/// <summary>
		/// List all <see cref="User"/>s
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in a <see cref="IReadOnlyList{T}"/> of all <see cref="User"/>s</returns>
		Task<IReadOnlyList<User>> List(CancellationToken cancellationToken);

		/// <summary>
		/// Create a new <paramref name="user"/>
		/// </summary>
		/// <param name="user">The <see cref="UserUpdate"/> used to create the new <see cref="User"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>The new <see cref="User"/></returns>
		Task<User> Create(UserUpdate user, CancellationToken cancellationToken);

		/// <summary>
		/// Update a <paramref name="user"/>
		/// </summary>
		/// <param name="user">The <see cref="UserUpdate"/> used to update the <see cref="User"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>The updated <see cref="User"/></returns>
		Task<User> Update(UserUpdate user, CancellationToken cancellationToken);
	}
}