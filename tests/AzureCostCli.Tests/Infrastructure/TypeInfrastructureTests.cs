using AzureCostCli.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AzureCostCli.Tests.Infrastructure;

public class TypeRegistrarTests
{
    private readonly IServiceCollection _serviceCollection;
    private readonly TypeRegistrar _registrar;

    public TypeRegistrarTests()
    {
        _serviceCollection = new ServiceCollection();
        _registrar = new TypeRegistrar(_serviceCollection);
    }

    [Fact]
    public void Register_RegistersServiceAndImplementation()
    {
        // Arrange
        var serviceType = typeof(ITestService);
        var implementationType = typeof(TestService);

        // Act
        _registrar.Register(serviceType, implementationType);

        // Assert
        var serviceDescriptor = _serviceCollection.FirstOrDefault(s => s.ServiceType == serviceType);
        serviceDescriptor.Should().NotBeNull();
        serviceDescriptor!.ImplementationType.Should().Be(implementationType);
        serviceDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void RegisterInstance_RegistersServiceInstance()
    {
        // Arrange
        var serviceType = typeof(ITestService);
        var instance = new TestService();

        // Act
        _registrar.RegisterInstance(serviceType, instance);

        // Assert
        var serviceDescriptor = _serviceCollection.FirstOrDefault(s => s.ServiceType == serviceType);
        serviceDescriptor.Should().NotBeNull();
        serviceDescriptor!.ImplementationInstance.Should().Be(instance);
        serviceDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void RegisterLazy_RegistersLazyFactory()
    {
        // Arrange
        var serviceType = typeof(ITestService);
        var instance = new TestService();
        Func<object> factory = () => instance;

        // Act
        _registrar.RegisterLazy(serviceType, factory);

        // Assert
        var serviceDescriptor = _serviceCollection.FirstOrDefault(s => s.ServiceType == serviceType);
        serviceDescriptor.Should().NotBeNull();
        serviceDescriptor!.ImplementationFactory.Should().NotBeNull();
        serviceDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void RegisterLazy_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceType = typeof(ITestService);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _registrar.RegisterLazy(serviceType, null!));
    }

    [Fact]
    public void Build_ReturnsTypeResolver()
    {
        // Act
        var resolver = _registrar.Build();

        // Assert
        resolver.Should().NotBeNull();
        resolver.Should().BeOfType<TypeResolver>();
    }

    // Test interfaces and classes
    private interface ITestService
    {
    }

    private class TestService : ITestService
    {
    }
}

public class TypeResolverTests
{
    [Fact]
    public void Constructor_WithNullProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TypeResolver(null!));
    }

    [Fact]
    public void Resolve_WithValidType_ReturnsService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();
        var resolver = new TypeResolver(provider);

        // Act
        var result = resolver.Resolve(typeof(ITestService));

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<TestService>();
    }

    [Fact]
    public void Resolve_WithNullType_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = new TypeResolver(provider);

        // Act
        var result = resolver.Resolve(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_WithUnregisteredType_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = new TypeResolver(provider);

        // Act
        var result = resolver.Resolve(typeof(ITestService));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Dispose_WithDisposableProvider_DisposesProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = new TypeResolver(provider);

        // Act & Assert - Should not throw
        resolver.Dispose();
    }

    [Fact]
    public void Dispose_WithNonDisposableProvider_DoesNotThrow()
    {
        // Arrange
        var mockProvider = Mock.Of<IServiceProvider>();
        var resolver = new TypeResolver(mockProvider);

        // Act & Assert - Should not throw
        resolver.Dispose();
    }

    // Test interfaces and classes
    private interface ITestService
    {
    }

    private class TestService : ITestService
    {
    }
}