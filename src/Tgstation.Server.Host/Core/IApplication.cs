﻿using System;

namespace Tgstation.Server.Host.Core
{
	/// <summary>
	/// Configures the ASP.NET Core web application
	/// </summary>
	public interface IApplication
	{
		/// <summary>
		/// Prefix to <see cref="VersionString"/>
		/// </summary>
		string VersionPrefix { get; }

		/// <summary>
		/// A more verbose version of <see cref="Version"/>
		/// </summary>
		string VersionString { get; }

		/// <summary>
		/// The version of the <see cref="Application"/>
		/// </summary>
		Version Version { get; }

		/// <summary>
		/// Mark the <see cref="IApplication"/> as ready to run
		/// </summary>
		/// <param name="initializationError">The <see cref="Exception"/> that put the application in a corrupted state if any</param>
		void Ready(Exception initializationError);
	}
}