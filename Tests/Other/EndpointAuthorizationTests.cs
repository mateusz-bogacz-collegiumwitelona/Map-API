using Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;

namespace Tests.Other
{
    public class EndpointAuthorizationTests
    {
        private static readonly Type[] ProfileControllers =
        {
            typeof(TripController),
            typeof(UserController)
        };

        private static readonly string[] MutationMethods =
        {
            "POST",
            "PUT",
            "PATCH",
            "DELETE"
        };

        private static IEnumerable<MethodInfo> GetActions(Type controller)
            => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());

        private static IEnumerable<string> GetHttpMethods(MethodInfo action)
            => action
                .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                .SelectMany(attribute => attribute.HttpMethods);

        [Test]
        public async Task ProfileControllers_ShouldRequireAuthenticationOnEveryAction()
        {
            // Act
            var unauthenticatedActions = ProfileControllers
                .SelectMany(controller => GetActions(controller)
                    .Where(action => !action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any())
                    .Select(action => $"{controller.Name}.{action.Name}"))
                .ToList();

            // Assert
            await Assert.That(unauthenticatedActions).IsEmpty();
        }

        [Test]
        public async Task ProfileControllers_ShouldNotExposeAnonymousMutationActions()
        {
            // Act
            var anonymousMutations = ProfileControllers
                .SelectMany(controller => GetActions(controller)
                    .Where(action => GetHttpMethods(action).Any(MutationMethods.Contains))
                    .Where(action => action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any() == false
                        || action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
                    .Select(action => $"{controller.Name}.{action.Name}"))
                .ToList();

            // Assert
            await Assert.That(anonymousMutations).IsEmpty();
        }

        [Test]
        public async Task MutationActionsOutsideAuth_ShouldRequireAuthentication()
        {
            // Arrange
            var controllers = typeof(TripController)
                .Assembly
                .GetTypes()
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type)
                    && !type.IsAbstract
                    && type != typeof(AuthController));

            // Act
            var unauthenticatedMutations = controllers
                .SelectMany(controller => GetActions(controller)
                    .Where(action => GetHttpMethods(action).Any(MutationMethods.Contains))
                    .Where(action => action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any() == false)
                    .Select(action => $"{controller.Name}.{action.Name}"))
                .ToList();

            // Assert
            await Assert.That(unauthenticatedMutations).IsEmpty();
        }
    }
}
