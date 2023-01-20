﻿// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.PluggableComponents.Adaptors;
using Dapr.PluggableComponents.Components.StateStore;

namespace Dapr.PluggableComponents;

/// <summary>
/// Context related to the creation of a Dapr Pluggable Component.
/// </summary>
/// <param name="InstanceId">The Dapr component ID (from its configuration) to associate with the Dapr Pluggable Component.</param>
/// <param name="ServiceProvider">A service provider from which to retrieve dependencies of the created Dapr Pluggable Component.</param>
/// <param name="SocketPath">The Unix domain socket file to associate with the Dapr Pluggable Component.</param>
public sealed record ComponentProviderContext(string? InstanceId, IServiceProvider ServiceProvider, string SocketPath);

/// <summary>
/// Represents a factory method that returns a Dapr Pluggable Component instance for a given Dapr component configuration and service socket.
/// </summary>
/// <typeparam name="T">The type of Dapr Pluggable Component being created.</typeparam>
/// <param name="context">The context related to creation.</param>
/// <returns>A Dapr Pluggable Component instance to be associated with the specified context.</returns>
public delegate T ComponentProviderDelegate<T>(ComponentProviderContext context);

/// <summary>
/// A builder for Dapr Pluggable Component services.
/// </summary>
public sealed class DaprPluggableComponentsServiceBuilder
{
    private readonly string socketPath;
    private readonly IDaprPluggableComponentsRegistrar registrar;

    internal DaprPluggableComponentsServiceBuilder(
        string socketPath,
        IDaprPluggableComponentsRegistrar registrar)
    {
        this.socketPath = socketPath;
        this.registrar = registrar;
    }

    /// <summary>
    /// Registers a singleton state store with this service.
    /// </summary>
    /// <typeparam name="TStateStore">The type of state store to register.</typeparam>
    /// <returns>The current <see cref="DaprPluggableComponentsServiceBuilder"/> instance.</returns>
    /// <remarks>
    /// A single instance of the state store will be created to service all configured Dapr components.
    ///
    /// Only a single state store type can be associated with a given service.
    /// </remarks>
    public DaprPluggableComponentsServiceBuilder RegisterStateStore<TStateStore>() where TStateStore : class, IStateStore
    {
        this.AddComponent<IStateStore, TStateStore, StateStoreAdaptor>();

        return this;
    }

    /// <summary>
    /// Registers a state store with this service.
    /// </summary>
    /// <typeparam name="TStateStore">The type of state store to register.</typeparam>
    /// <param name="stateStoreFactory">A factory method called when creating new state store instances.</param>
    /// <returns>The current <see cref="DaprPluggableComponentsServiceBuilder"/> instance.</returns>
    /// <remarks>
    /// The factory method will be called once for each configured Dapr component; the returned instance will be
    /// associated with that Dapr component and methods invoked when the component receives requests.
    ///
    /// Only a single state store type can be associated with a given service.
    /// </remarks>
    public DaprPluggableComponentsServiceBuilder RegisterStateStore<TStateStore>(ComponentProviderDelegate<TStateStore> stateStoreFactory)
        where TStateStore : class, IStateStore
    {
        this.AddComponent<IStateStore, TStateStore, StateStoreAdaptor>(stateStoreFactory);

        return this;
    }

    private void AddComponent<TComponentType, TComponentImpl, TAdaptor>()
        where TComponentType : class
        where TComponentImpl : class, TComponentType
        where TAdaptor : class
    {
        this.registrar.RegisterComponent<TComponentImpl>(this.socketPath);

        this.AddRelatedService<TComponentType, TComponentImpl, TAdaptor>();
    }

    private void AddComponent<TComponentType, TComponentImpl, TAdaptor>(ComponentProviderDelegate<TComponentImpl> pubSubFactory)
        where TComponentType : class
        where TComponentImpl : class, TComponentType
        where TAdaptor : class
    {
        this.registrar.RegisterComponent<TComponentImpl>(socketPath, pubSubFactory);

        this.AddRelatedService<TComponentType, TComponentImpl, TAdaptor>();
    }

    private void AddRelatedService<TComponent, TComponentImpl, TAdaptor>()
        where TComponent : class
        where TComponentImpl : class
        where TAdaptor : class
    {
        this.registrar.RegisterProvider<TComponent, TComponentImpl>(this.socketPath);

        this.registrar.RegisterAdaptor<TAdaptor>();
    }
}
