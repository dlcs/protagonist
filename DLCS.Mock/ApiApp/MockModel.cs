using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.HydraModel;

namespace DLCS.Mock.ApiApp
{
    public class MockModel
    {
        // Entities
        public List<Customer> Customers { get; set; }
        public List<PortalUser> PortalUsers { get; set; }
        public List<NamedQuery> NamedQueries { get; set; }
        public List<OriginStrategy> OriginStrategies { get; set; }
        public List<ThumbnailPolicy> ThumbnailPolicies { get; set; } 
        public List<ImageOptimisationPolicy> ImageOptimisationPolicies { get; set; } 
        public List<PortalRole> PortalRoles { get; set; }
        public List<CustomerOriginStrategy> CustomerOriginStrategies { get; set; }
        public List<AuthService> AuthServices { get; set; }
        public List<RoleProvider> RoleProviders { get; set; }
        public List<Role> Roles { get; set; }
        public List<Space> Spaces { get; set; }
        public List<Queue> Queues { get; set; }
        public List<Image> Images { get; set; }  
        public List<Batch> Batches { get; set; }

        // collections that can't be generated from entities alone
        public Dictionary<string, List<string>> AuthServiceParentChild { get; set; } 
        public Dictionary<string, string> RoleAuthService { get; set; } 
        public Dictionary<string, List<string>> SpaceDefaultRoles { get; set; }
        public Dictionary<string, List<string>> ImageRoles { get; set; }
        public Dictionary<string, List<string>> BatchImages { get; set; }
        public Dictionary<string, List<string>> PortalUserRoles { get; set; }
        
        public readonly object ModelLock = new object();

        public string BaseUrl { get; set; }

        public void Init(string baseUrl)
        {
            BaseUrl = baseUrl;
            var customers = CreateCustomers();
            Customers = customers;
            OriginStrategies = CreateOriginStrategies();
            PortalRoles = CreatePortalRoles();
            ImageOptimisationPolicies = CreateImageOptimisationPolicies();
            ThumbnailPolicies = CreateThumbnailPolicies();
            PortalUserRoles = new Dictionary<string, List<string>>();
            PortalUsers = CreatePortalUsers(customers, PortalRoles, PortalUserRoles);
            NamedQueries = CreateNamedQueries(customers);
            CustomerOriginStrategies = CreateCustomerOriginStrategies(customers, OriginStrategies);
            AuthServiceParentChild = new Dictionary<string, List<string>>();
            AuthServices = CreateAuthServices(customers, AuthServiceParentChild);
            RoleProviders = CreateRoleProviders(AuthServices);
            RoleAuthService = new Dictionary<string, string>();
            var roles = CreateRoles(customers, AuthServices, RoleAuthService);
            Roles = roles;
            SpaceDefaultRoles = new Dictionary<string, List<string>>();
            var spaces = CreateSpaces(customers, roles, SpaceDefaultRoles);
            Spaces = spaces;
            Queues = CreateQueues(customers);
            ImageRoles = new Dictionary<string, List<string>>();
            var images = CreateImages(spaces, SpaceDefaultRoles, ImageRoles, ImageOptimisationPolicies, ThumbnailPolicies);
            Images = images;
            BatchImages = new Dictionary<string, List<string>>();
            Batches = CreateBatches(images, BatchImages);
            RecalculateCounters();
        }

        private List<ThumbnailPolicy> CreateThumbnailPolicies()
        {
            return new List<ThumbnailPolicy>
            {
                new ThumbnailPolicy(BaseUrl, "standard", "standard DLCS thumbs", new[] {1024, 400, 200, 100})
            };
        }

        private List<ImageOptimisationPolicy> CreateImageOptimisationPolicies()
        {
            return new List<ImageOptimisationPolicy>
            {
                new ImageOptimisationPolicy(BaseUrl, "fast_lossy", "Fast lossy", "kdu_1")
            };
        }

        private List<PortalRole> CreatePortalRoles()
        {
            return new List<PortalRole>
            {
                new PortalRole(BaseUrl, "admin", "Administrator"),
                new PortalRole(BaseUrl, "readonly", "Read only"),
                new PortalRole(BaseUrl, "samplerole", "Another example role")
            };
        }

        private List<OriginStrategy> CreateOriginStrategies()
        {
            return new List<OriginStrategy>
            {
                new OriginStrategy(BaseUrl, "default", "No credentials over http/s", false),
                new OriginStrategy(BaseUrl, "basic_https", "Basic Auth over https", true),
                new OriginStrategy(BaseUrl, "ftps_creds", "FTPS with credentials", true),
                new OriginStrategy(BaseUrl, "s3", "Fetch from s3 bucket presenting DLCS identity", true),
            };
        }

        private List<Batch> CreateBatches(List<Image> images, Dictionary<string, List<string>> batchImages)
        {
            var r = new Random();
            var batches = new List<Batch>();
            int batchId = 100001;
            Batch currentBatch = null;
            int batchSize = -1;
            int counter = -1;
            int currentCustomer = -1;
            List<string> imagesInBatch = null;
            foreach (var image in images)
            {
                if (counter++ > batchSize || image.CustomerId != currentCustomer)
                {
                    // save the old batch
                    if (currentBatch != null)
                    {
                        batches.Add(currentBatch);
                        batchImages.Add(currentBatch.Id, imagesInBatch);
                    }
                    // start a new batch
                    currentCustomer = image.CustomerId;
                    counter = 1;
                    batchSize = r.Next(3, 10);
                    var created = image.Created ?? DateTime.Now;
                    currentBatch = new Batch(BaseUrl, batchId++, image.CustomerId, created.AddSeconds(-1));
                    imagesInBatch = new List<string>();
                }
                imagesInBatch.Add(image.Id);
                image.Batch = currentBatch.Id;
            }
            batches.Add(currentBatch);
            batchImages.Add(currentBatch.Id, imagesInBatch);
            return batches;
        }
        
        private List<Image> CreateImages(List<Space> spaces, 
            Dictionary<string, List<string>> spaceDefaultRoles, Dictionary<string, List<string>> imageRoles,
            List<ImageOptimisationPolicy> imageOptimisationPolicies, List<ThumbnailPolicy> thumbnailPolicies  )
        {
            var images = new List<Image>();
            foreach (var space in spaces)
            {
                images.AddRange(MakeImagesForSpace(20, space, spaceDefaultRoles, imageRoles, imageOptimisationPolicies, thumbnailPolicies));
            }
            return images;
        }

        private List<Image> MakeImagesForSpace(int howMany, Space space, 
            Dictionary<string, List<string>> spaceDefaultRoles, Dictionary<string, List<string>> imageRoles,
            List<ImageOptimisationPolicy> imageOptimisationPolicies, List<ThumbnailPolicy> thumbnailPolicies)
        {
            Random r = new Random();
            var images = new List<Image>();
            var ongoing = space.ModelId%2 == 0;
            var queued = ongoing ? DateTime.Now.AddHours(-4) : new DateTime(2015, 11, 30); 
            for (int i = 0; i < howMany; i++)
            {
                DateTime? dequeued = ongoing ? (DateTime?) null : queued.AddHours(1).AddSeconds(i * 5);
                if (ongoing && i < 6) dequeued = DateTime.Now.AddSeconds(-80 + 9*i);
                DateTime? finished = ongoing ? (DateTime?)null : queued.AddSeconds(3608).AddSeconds(i * 7);
                if (ongoing && i < 4) finished = DateTime.Now.AddSeconds(-60 + 9 * i);
                var id = Guid.NewGuid().ToString().Substring(0, 8) + i.ToString().PadLeft(5, '0');
                var image = MockHelp.MakeImage(BaseUrl, space.CustomerId, space.ModelId, id,
                    DateTime.Now, "https://customer.com/images/" + id + ".tiff", null,
                    r.Next(2000,11000), r.Next(3000,11000), space.DefaultMaxUnauthorised,
                    queued, dequeued, finished, !finished.HasValue, null,
                    space.DefaultTags, "b12345678", null, null, i, 0, 0,
                    imageOptimisationPolicies.First().Id, thumbnailPolicies.First().Id);
                images.Add(image);
                if (spaceDefaultRoles.ContainsKey(space.Id))
                {
                    var roles = spaceDefaultRoles[space.Id];
                    if (roles.Any())
                    {
                        imageRoles.Add(image.Id, roles);
                    }
                }
            }
            return images;
        }


        private List<Customer> CreateCustomers()
        {
            return new List<Customer>
            {
                new Customer(BaseUrl, 1, "admin", "Administrator"),
                new Customer(BaseUrl, 2, "wellcome", "Wellcome"),
                new Customer(BaseUrl, 3, "crane", "Crane"),
                new Customer(BaseUrl, 4, "iiifly", "IIIF.ly")
            };
        }

        private List<PortalUser> CreatePortalUsers(List<Customer> customers, List<PortalRole> portalRoles, Dictionary<string, List<string>> portalUserRoles)
        {
            var portalUsers = new List<PortalUser>
            {
                new PortalUser(BaseUrl, customers.GetByName("admin").ModelId, 
                    "8b083aee", "adam.christie@digirati.co.uk", new DateTime(2005, 10, 31), true),
                new PortalUser(BaseUrl, customers.GetByName("admin").ModelId,
                    "e3afdce8", "admin@dlcs.io", new DateTime(2016, 1, 1), true),
                new PortalUser(BaseUrl, customers.GetByName("wellcome").ModelId,
                    "ef132a3f", "r.kiley@wellcome.ac.uk", new DateTime(1961, 10, 31), true),
                new PortalUser(BaseUrl, customers.GetByName("iiifly").ModelId,
                    "9cee79e8", "tom.crane@digirati.co.uk", new DateTime(2010, 6, 21), true)
            };

            portalUserRoles.Add(portalUsers[0].Id, new List<string> { portalRoles.Single(pr => pr.ModelId == "admin").Id });
            portalUserRoles.Add(portalUsers[1].Id, new List<string> { portalRoles.Single(pr => pr.ModelId == "admin").Id });
            portalUserRoles.Add(portalUsers[2].Id, new List<string> { portalRoles.Single(pr => pr.ModelId == "samplerole").Id });
            portalUserRoles.Add(portalUsers[3].Id, new List<string> { portalRoles.Single(pr => pr.ModelId == "readonly").Id });

            return portalUsers;
        }

        private List<NamedQuery> CreateNamedQueries(List<Customer> customers)
        {
            return new List<NamedQuery>
            {
                MockHelp.MakeNamedQuery(BaseUrl, customers.GetByName("iiifly").ModelId, "nq1", "bob", false, "template1-here"),
                MockHelp.MakeNamedQuery(BaseUrl, customers.GetByName("iiifly").ModelId, "nq2", "manifest", false, "template2-here")
            };
        }


        private List<CustomerOriginStrategy> CreateCustomerOriginStrategies(List<Customer> customers, List<OriginStrategy> originStrategies)
        {
            return new List<CustomerOriginStrategy>
            {
                MockHelp.MakeCustomerOriginStrategy(BaseUrl, customers.GetByName("wellcome").ModelId, 
                    101, "https://wellcomelibrary.org/service/asset(.+)", "s3://wellcome/path-to-origin-creds", 
                    originStrategies.Single(os => os.ModelId == "basic_https").Id),
                MockHelp.MakeCustomerOriginStrategy(BaseUrl, customers.GetByName("iiifly").ModelId,
                    102, "https://example.org/images/(.+)", "s3://test/path-to-origin-creds", 
                    originStrategies.Single(os => os.ModelId == "basic_https").Id),
                MockHelp.MakeCustomerOriginStrategy(BaseUrl, customers.GetByName("iiifly").ModelId,
                    103, "ftps://example.org/images/(.+)", "s3://test/path-to-ftp-creds",
                    originStrategies.Single(os => os.ModelId == "ftps_creds").Id)
            };
        }
        
        private List<AuthService> CreateAuthServices(List<Customer> customers, Dictionary<string, List<string>> authServiceParentChild)
        {
            int wellcome = customers.GetByName("wellcome").ModelId;
            int iiifly = customers.GetByName("iiifly").ModelId;
            var authServices = new List<AuthService>
            {
                MockHelp.MakeAuthService(BaseUrl, wellcome, "wellcome-clickthrough-login", "clickthrough", "http://iiif.io/api/auth/0/login", 0,
                    "Terms and Conditions", "<p>clickthrough...</p>", 
                    "Terms and Conditions", "<p>More detailed info</p>", "Accept terms"),
                MockHelp.MakeAuthService(BaseUrl, wellcome, "wellcome-clickthrough-token", "clickthrough-token", "http://iiif.io/api/auth/0/token", 1800,
                    "token service", null, null, null, null),
                MockHelp.MakeAuthService(BaseUrl, wellcome, "wellcome-clickthrough-logout", "clickthrough-logout", "http://iiif.io/api/auth/0/logout", 0,
                    "Forget terms", null, null, null, null),

                MockHelp.MakeAuthService(BaseUrl, wellcome, "wellcome-delegated-login", "delegated-login", "http://iiif.io/api/auth/0/login", 0,
                    "Log in to view protected material", "<p>More detailed text for login prompt in UV</p>", 
                    null, null, "Log in"),
                MockHelp.MakeAuthService(BaseUrl, wellcome, "wellcome-delegated-token", "delegated-token", "http://iiif.io/api/auth/0/token", 1800,
                    "token service", null, null, null, null),
                MockHelp.MakeAuthService(BaseUrl, wellcome, "wellcome-delegated-logout", "delegated-logout", "http://iiif.io/api/auth/0/logout", 0,
                    "Log out", null, null, null, null),

                MockHelp.MakeAuthService(BaseUrl, iiifly, "iiifly-clickthrough-login", "clickthrough", "http://iiif.io/api/auth/0/login", 0,
                    "Terms and Conditions", "<p>clickthrough...</p>",
                    "Terms and Conditions", "<p>More detailed info</p>", "Accept terms"),
                MockHelp.MakeAuthService(BaseUrl, iiifly, "iiifly-clickthrough-token", "clickthrough-token", "http://iiif.io/api/auth/0/token", 1800,
                    "token service", null, null, null, null),
                MockHelp.MakeAuthService(BaseUrl, iiifly, "iiifly-clickthrough-logout", "clickthrough-logout", "http://iiif.io/api/auth/0/logout", 0,
                    "Forget terms", null, null, null, null),
            };

            authServiceParentChild.Add(
                    authServices.GetByIdPart("wellcome-clickthrough-login").Id,
                        new List<string> {  authServices.GetByIdPart("wellcome-clickthrough-token").Id,
                                            authServices.GetByIdPart("wellcome-clickthrough-logout").Id });

            authServiceParentChild.Add(
                    authServices.GetByIdPart("wellcome-delegated-login").Id,
                        new List<string> {  authServices.GetByIdPart("wellcome-delegated-token").Id,
                                            authServices.GetByIdPart("wellcome-delegated-logout").Id });

            authServiceParentChild.Add(
                    authServices.GetByIdPart("iiifly-clickthrough-login").Id,
                        new List<string> {  authServices.GetByIdPart("iiifly-clickthrough-token").Id,
                                            authServices.GetByIdPart("iiifly-clickthrough-logout").Id });

            return authServices;
        }


        private List<RoleProvider> CreateRoleProviders(List<AuthService> authServices)
        {
            var wellcomeDelegated = authServices.GetByIdPart("wellcome-delegated-login");
            return new List<RoleProvider>
            {
                new RoleProvider(BaseUrl, wellcomeDelegated.CustomerId, wellcomeDelegated.ModelId, "{ Some CAS or OAuth details }", 
                    "s3://wellcome/path-to-sso-backchannel-creds-if-required")
            };
        }

        private List<Role> CreateRoles(List<Customer> customers, List<AuthService> authServices, Dictionary<string, string> roleAuthService)
        {
            int wellcome = customers.GetByName("wellcome").ModelId;
            int iiifly = customers.GetByName("iiifly").ModelId;
            
            var roles = new List<Role>
            {
                new Role(BaseUrl, wellcome, "clickthrough", "Click through",
                    "Role for DLCS-enforced auth with no delegation", new [] { "Requires Registration", "reqreg"}),
                new Role(BaseUrl, wellcome, "clinical", "Clinical Delegate to wellcomelibrary.org",
                    "Role for DLCS-enforced auth with delegation to customer", new [] { "Clinical Images", "Healthcare professional" }),
                new Role(BaseUrl, wellcome, "staff", "Staff Delegate to wellcomelibrary.org",
                    "Role for DLCS-enforced auth with delegation to customer", new [] { "Wellcome Staff Member" }),
                new Role(BaseUrl, wellcome, "restricted", "Restricted Delegate to wellcomelibrary.org",
                    "Role for DLCS-enforced auth with delegation to customer", new [] { "restricted" }),

                new Role(BaseUrl, iiifly, "clickthrough", "Click through",
                    "Role for DLCS-enforced auth with no delegation", new [] { "acceptterms" }),
            };

            roleAuthService.Add(roles[0].Id, authServices.GetByIdPart("wellcome-clickthrough-login").Id);
            roleAuthService.Add(roles[1].Id, authServices.GetByIdPart("wellcome-delegated-login").Id);
            roleAuthService.Add(roles[2].Id, authServices.GetByIdPart("wellcome-delegated-login").Id);
            roleAuthService.Add(roles[3].Id, authServices.GetByIdPart("wellcome-delegated-login").Id);
            roleAuthService.Add(roles[4].Id, authServices.GetByIdPart("iiifly-clickthrough-login").Id);
            return roles;
        }


        private List<Space> CreateSpaces(List<Customer> customers, List<Role> roles, Dictionary<string, List<string>> spaceDefaultRoles)
        {
            int wellcome = customers.GetByName("wellcome").ModelId;
            int iiifly = customers.GetByName("iiifly").ModelId;
            var spaces = new List<Space>
            {
                new Space(BaseUrl, 1, wellcome, "wellcome1", DateTime.Now, null, -1),
                new Space(BaseUrl, 2, wellcome, "wellcome2", DateTime.Now, null, -1),
                new Space(BaseUrl, 11, iiifly, "iiifly1", DateTime.Now, null, 400),
                new Space(BaseUrl, 12, iiifly, "iiifly2", DateTime.Now, new [] {"tag1", "tag2"}, -1)
            };
             
            spaceDefaultRoles.Add(
                    spaces[1].Id,
                        new List<string> { roles.GetByCustAndId(wellcome, "clinical").Id });
            spaceDefaultRoles.Add(
                    spaces[3].Id,
                        new List<string> { roles.GetByCustAndId(iiifly, "clickthrough").Id });

            return spaces;
        }

        public void RecalculateCounters()
        {
            lock (ModelLock)
            {
                SetBatchCounts();
                SetQueueSizes();
            }
        }

        private void SetBatchCounts()
        {
            foreach (var batch in Batches)
            {
                var imageIds = BatchImages[batch.Id];
                var images = Images.Where(i => imageIds.Contains(i.Id)).ToList();
                batch.Count = images.Count;
                batch.Completed = images.Count(i => i.Finished.HasValue);
                if (images.All(i => i.Finished.HasValue))
                {
                    batch.Finished = images.Select(i => i.Finished).Max();
                }
                else
                {
                    batch.EstCompletion = DateTime.Now.AddMinutes(3);
                }
            }
        }

        private void SetQueueSizes()
        {
            var totalByCustomer = new Dictionary<int, int>();
            foreach (var image in Images)
            {
                if (!totalByCustomer.ContainsKey(image.CustomerId))
                {
                    totalByCustomer[image.CustomerId] = 0;
                }
                if (!image.Finished.HasValue)
                {
                    totalByCustomer[image.CustomerId] += 1;
                }
            }
            foreach (var queue in Queues)
            {
                if (totalByCustomer.ContainsKey(queue.ModelId))
                {
                    queue.Size = totalByCustomer[queue.ModelId];
                }
            }
        }


        private List<Queue> CreateQueues(List<Customer> customers)
        {
            return new List<Queue>
            {
                new Queue(BaseUrl, 1),
                new Queue(BaseUrl, 2),
                new Queue(BaseUrl, 3),
                new Queue(BaseUrl, 4)
            };
        }
    }
}
