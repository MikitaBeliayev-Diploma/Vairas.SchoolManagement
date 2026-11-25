extern alias Authentication;
extern alias UserManagement;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using Vairas.Authentication.Business;
using Vairas.Authentication.Domain;
using Vairas.Authentication.Persistance;
using Vairas.UserManagement.Persistance.DataAccess;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
using Vairas.SchoolManagement.Persistance;
using AuthProgram = Authentication::Program;
using UserProgram = UserManagement::Program;
using AuthHasher = Authentication::Vairas.Authentication.Security.PasswordHasher;
using AuthProtector = Vairas.Authentication.Business.ISecretProtector;
using UmDependencies = UserManagement::Vairas.UserManagement.Configuration.Options.DependencyOptions;
using UmJwt = UserManagement::Vairas.UserManagement.Configuration.Options.JwtOptions;
using UmOutbox = UserManagement::Vairas.UserManagement.Configuration.Options.OutboxOptions;
using Vairas.SchoolManagement.Configuration.Options;

namespace Vairas.SchoolManagement.Tests;

public sealed class ConfiguredFactory<T>(string contentRoot, Dictionary<string, string?> config, Action<IServiceCollection>? services = null) : WebApplicationFactory<T> where T : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(contentRoot);
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(config));
        builder.ConfigureLogging(l => l.ClearProviders());
        if (services is not null)
            builder.ConfigureTestServices(services);
    }
}
