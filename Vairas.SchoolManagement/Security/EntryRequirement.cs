using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Middleware;
using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Security;


public sealed record EntryRequirement(string? Scope = null) : IAuthorizationRequirement;
