using System.Collections.Concurrent;
using RZ.Foundation.Extensions;
using Tirax.Application.WireMocker.Domain;

namespace Tirax.Application.WireMocker.Services;

public interface IDataStore
{
    OutcomeT<Synchronous, IAsyncEnumerable<Service>> GetServices();

    OutcomeT<Asynchronous, Service>      Save(Service service);
    OutcomeT<Asynchronous, Seq<Service>> Search(string name);
}

public sealed class InMemoryDataStore : IDataStore
{
    readonly ConcurrentDictionary<Guid, Service> services = new();
    readonly ConcurrentDictionary<Guid, ServiceSetting> serviceSettings = new();

    public OutcomeT<Synchronous, IAsyncEnumerable<Service>> GetServices() =>
        Success(services.Values.ToArray().AsAsyncEnumerable());

    public OutcomeT<Asynchronous, Service> Save(Service service) {
        var invalidReason = Service.Validator.Validate(service).Errors.Map(e => e.ToString()).HeadOrNone();
        return invalidReason.IfSome(out var error)
                   ? FailureAsync<Service>(StandardErrors.UnexpectedError($"Invalid service data: {error}"))
                   : SuccessAsync(services[service.Id] = service);
    }

    public OutcomeT<Asynchronous, Seq<Service>> Search(string name) {
        var result = services.Values.Where(s => s.Name.Contains(name)).ToSeq();
        return SuccessAsync(result);
    }
}