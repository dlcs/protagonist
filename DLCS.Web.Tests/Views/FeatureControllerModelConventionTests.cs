using System.Collections.Generic;
using System.Reflection;
using DLCS.Web.Views;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Project.Features.Home;
using Xunit;

namespace DLCS.Web.Tests.Views
{
    public class FeatureControllerModelConventionTests
    {
        [Fact]
        public void FeatureProperty_SetCorrectly()
        {
            var service = new FeatureControllerModelConvention();
            var model = new ControllerModel(typeof(HomeController).GetTypeInfo(), new List<string>());

            service.Apply(model);

            model.Properties["feature"].Should().Be(@"Features\Home");
        }
    }
}

namespace Project.Features.Home
{
    internal class HomeController
    {       
    }
}