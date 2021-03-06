﻿using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Host.Models;

namespace Tgstation.Server.Host.Core
{
	/// <summary>
	/// Manages the runtime of <see cref="Job"/>s
	/// </summary>
	public interface IJobManager : IHostedService
	{
		/// <summary>
		/// Get the <see cref="Api.Models.Job.Progress"/> for a <paramref name="job"/>
		/// </summary>
		/// <param name="job">The <see cref="Job"/> to get <see cref="Api.Models.Job.Progress"/> for</param>
		/// <returns>The <see cref="Api.Models.Job.Progress"/> of <paramref name="job"/></returns>
		int? JobProgress(Job job);

		/// <summary>
		/// Registers a given <see cref="Job"/> and begins running it
		/// </summary>
		/// <param name="job">The <see cref="Job"/></param>
		/// <param name="operation">The operation to run taking the started <see cref="Job"/>, a <see cref="IDatabaseContext"/>, progress reporter <see cref="Action{T1}"/> and a <see cref="CancellationToken"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing a running operation</returns>
		Task RegisterOperation(Job job, Func<Job, IDatabaseContext, Action<int>, CancellationToken, Task> operation, CancellationToken cancellationToken);

		/// <summary>
		/// Wait for a given <paramref name="job"/> to complete
		/// </summary>
		/// <param name="job">The <see cref="Job"/> to wait for </param>
		/// <param name="canceller">The <see cref="User"/> to cancel the <paramref name="job"/></param>
		/// <param name="jobCancellationToken">A <see cref="CancellationToken"/> that will cancel the <paramref name="job"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the <see cref="Job"/></returns>
#pragma warning disable CA1068 // CancellationToken parameters must come last https://github.com/dotnet/roslyn-analyzers/issues/1816
		Task WaitForJobCompletion(Job job, User canceller, CancellationToken jobCancellationToken, CancellationToken cancellationToken);
#pragma warning restore CA1068 // CancellationToken parameters must come last

		/// <summary>
		/// Cancels a give <paramref name="job"/>
		/// </summary>
		/// <param name="job">The <see cref="Job"/> to cancel</param>
		/// <param name="user">The <see cref="User"/> who cancelled the <paramref name="job"/></param>
		/// <param name="blocking">If the operation should wait until the job exits before completing</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in <see langword="true"/> if the <paramref name="job"/> was cancelled, <see langword="false"/> if it couldn't be found</returns>
		Task<bool> CancelJob(Job job, User user, bool blocking, CancellationToken cancellationToken);
	}
}
