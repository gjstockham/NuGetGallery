﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Authentication;
using Xunit;

namespace NuGetGallery
{
    public class CuratedFeedsControllerFacts
    {
        public class TestableCuratedFeedsController : CuratedFeedsController
        {
            public TestableCuratedFeedsController()
            {
                StubCuratedFeed = new CuratedFeed
                    { Key = 0, Name = "aName", Managers = new HashSet<User>(new[] { new User { Username = "aUsername" } }) };
                StubCuratedFeedService = new Mock<ICuratedFeedService>();
            
                StubCuratedFeedService
                    .Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns(StubCuratedFeed);

                CuratedFeedService = StubCuratedFeedService.Object;

                StubSearchService = new Mock<ISearchService>();
                SearchService = StubSearchService.Object;

                var httpContext = new Mock<HttpContextBase>();
                TestUtility.SetupHttpContextMockForUrlGeneration(httpContext, this);
                this.SetUser("aUsername");
            }

            public CuratedFeed StubCuratedFeed { get; set; }
            public Mock<ICuratedFeedService> StubCuratedFeedService { get; private set; }
            public Mock<ISearchService> StubSearchService { get; private set; }
            
            protected internal override T GetService<T>()
            {
                if (typeof(T) == typeof(ICuratedFeedService))
                {
                    return (T)StubCuratedFeedService.Object;
                }

                throw new Exception("Tried to get an unexpected service.");
            }
        }

        public class TheGetCuratedFeedAction
        {
            [Fact]
            public void WillReturn404IfTheCuratedFeedDoesNotExist()
            {
                var controller = new TestableCuratedFeedsController();
                controller.StubCuratedFeedService.Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>())).Returns((CuratedFeed)null);

                var result = controller.CuratedFeed("aFeedName");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WillReturn403IfTheCurrentUsersIsNotAManagerOfTheCuratedFeed()
            {
                var controller = new TestableCuratedFeedsController();
                controller.SetUser("notAManager");
                
                var result = controller.CuratedFeed("aFeedName") as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(403, result.StatusCode);
            }

            [Fact]
            public void WillPassTheCuratedFeedNameToTheView()
            {
                var controller = new TestableCuratedFeedsController();
                controller.StubCuratedFeed.Name = "theCuratedFeedName";

                var viewModel = (controller.CuratedFeed("aFeedName") as ViewResult).Model as CuratedFeedViewModel;

                Assert.NotNull(viewModel);
                Assert.Equal("theCuratedFeedName", viewModel.Name);
            }

            [Fact]
            public void WillPassTheCuratedFeedManagersToTheView()
            {
                var controller = new TestableCuratedFeedsController();
                controller.SetUser("theManager");
                controller.StubCuratedFeed.Name = "aFeedName";
                controller.StubCuratedFeed.Managers = new HashSet<User>(new[] { new User { Username = "theManager" } });

                var viewModel = (controller.CuratedFeed("aFeedName") as ViewResult).Model as CuratedFeedViewModel;

                Assert.NotNull(viewModel);
                Assert.Equal("theManager", viewModel.Managers.First());
            }

            [Fact]
            public void WillPassTheIncludedPackagesToTheView()
            {
                var controller = new TestableCuratedFeedsController();
                controller.StubCuratedFeed.Packages = new HashSet<CuratedPackage>(
                    new[]
                        {
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = true,
                                    Included = true,
                                    PackageRegistration = new PackageRegistration { Id = "theAutomaticallyCuratedId" }
                                },
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = false,
                                    Included = true,
                                    PackageRegistration = new PackageRegistration { Id = "theManuallyCuratedId" }
                                },
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = true,
                                    Included = false,
                                    PackageRegistration = new PackageRegistration { Id = "theExcludedId" }
                                }
                        });

                var viewModel = (controller.CuratedFeed("aFeedName") as ViewResult).Model as CuratedFeedViewModel;

                Assert.NotNull(viewModel);
                Assert.Equal(2, viewModel.IncludedPackages.Count());
                Assert.Equal("theAutomaticallyCuratedId", viewModel.IncludedPackages.ElementAt(0).Id);
                Assert.Equal("theManuallyCuratedId", viewModel.IncludedPackages.ElementAt(1).Id);
            }

            [Fact]
            public void WillPassTheExcludedPackagesToTheView()
            {
                var controller = new TestableCuratedFeedsController();
                controller.StubCuratedFeed.Packages = new HashSet<CuratedPackage>(
                    new[]
                        {
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = true,
                                    Included = true,
                                    PackageRegistration = new PackageRegistration { Id = "theAutomaticallyCuratedId" }
                                },
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = false,
                                    Included = true,
                                    PackageRegistration = new PackageRegistration { Id = "theManuallyCuratedId" }
                                },
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = true,
                                    Included = false,
                                    PackageRegistration = new PackageRegistration { Id = "theExcludedId" }
                                }
                        });

                var viewModel = (controller.CuratedFeed("aFeedName") as ViewResult).Model as CuratedFeedViewModel;

                Assert.NotNull(viewModel);
                Assert.Equal(1, viewModel.ExcludedPackages.Count());
                Assert.Equal("theExcludedId", viewModel.ExcludedPackages.First());
            }
        }

        public class TheListPackagesAction
        {
            [Fact]
            public void WillSearchForAPackage()
            {
                var controller = new TestableCuratedFeedsController();

                var redPill = new PackageRegistration
                {
                    Id = "RedPill",
                    Key = 2,
                    DownloadCount = 0,
                    Packages = new []
                    {
                        new Package
                        {
                            Key = 89932,
                        }
                    },
                    Owners = new [] 
                    {
                        new User
                        {
                            Key = 66,
                            Username = "Morpheus",
                        }
                    }
                };

                redPill.Packages.ElementAt(0).PackageRegistration = redPill;

                var mockPackageRegistrations = new [] { redPill }.AsQueryable();
                var mockPackages = new[] { redPill.Packages.ElementAt(0) }.AsQueryable();

                controller.StubCuratedFeedService
                    .Setup(stub => stub.GetKey("TheMatrix"))
                    .Returns(2);

                int totalHits;
                controller.StubSearchService
                    .Setup(stub => stub.Search(It.IsAny<SearchFilter>(), out totalHits))
                    .Returns(mockPackages);

                var mockHttpContext = new Mock<HttpContextBase>();
                TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                // Act
                var result = controller.ListPackages("TheMatrix", "");

                Assert.IsType<ViewResult>(result);
                Assert.IsType<PackageListViewModel>(((ViewResult)result).Model);
                var model = (result as ViewResult).Model as PackageListViewModel;
                Assert.Equal(1, model.Items.Count());
            }
        }
    }
}
