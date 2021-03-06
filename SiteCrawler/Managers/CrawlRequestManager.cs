#region License : arachnode.net

// // Copyright (c) 2015 http://arachnode.net, arachnode.net, LLC
// //  
// // Permission is hereby granted, upon purchase, to any person
// // obtaining a copy of this software and associated documentation
// // files (the "Software"), to deal in the Software without
// // restriction, including without limitation the rights to use,
// // copy, merge and modify copies of the Software, and to permit persons
// // to whom the Software is furnished to do so, subject to the following
// // conditions:
// // 
// // LICENSE (ALL VERSIONS/EDITIONS): http://arachnode.net/r.ashx?3
// // 
// // The above copyright notice and this permission notice shall be
// // included in all copies or substantial portions of the Software.
// // 
// // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// // EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// // OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// // NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// // HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// // WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// // FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// // OTHER DEALINGS IN THE SOFTWARE.

#endregion

#region

using Arachnode.Configuration;
using Arachnode.DataAccess;
using Arachnode.DataAccess.Value.Interfaces;
using Arachnode.Performance;
using Arachnode.Renderer.Value.Enums;
using Arachnode.SiteCrawler.Components;
using Arachnode.SiteCrawler.Core;
using Arachnode.SiteCrawler.Value;
using Arachnode.SiteCrawler.Value.AbstractClasses;
using Arachnode.SiteCrawler.Value.Enums;

#endregion

namespace Arachnode.SiteCrawler.Managers
{
    public class CrawlRequestManager<TArachnodeDAO> : ACrawlRequestManager<TArachnodeDAO> where TArachnodeDAO : IArachnodeDAO
    {
        //ANODET: Check out the single-multiline options for the Regexes in the app.

        public CrawlRequestManager(ApplicationSettings applicationSettings, WebSettings webSettings, Cache<TArachnodeDAO> cache, ConsoleManager<TArachnodeDAO> consoleManager, ADiscoveryManager<TArachnodeDAO> discoveryManager) : base(applicationSettings, webSettings, cache, consoleManager, discoveryManager)
        {
        }

        /// <summary>
        /// 	Processes the crawl request.
        /// </summary>
        /// <param name = "crawlRequest">The crawl request.</param>
        /// <param name = "fileManager">The file manager.</param>
        /// <param name = "imageManager">The image manager.</param>
        /// <param name = "webPageManager">The web page manager.</param>
        /// <param name = "arachnodeDAO">The arachnode DAO.</param>
        public override void ProcessCrawlRequest(CrawlRequest<TArachnodeDAO> crawlRequest, FileManager<TArachnodeDAO> fileManager, ImageManager<TArachnodeDAO> imageManager, WebPageManager<TArachnodeDAO> webPageManager, IArachnodeDAO arachnodeDAO)
        {
            switch (crawlRequest.DataType.DiscoveryType)
            {
                case DiscoveryType.File:
                    ProcessFile(crawlRequest, fileManager, arachnodeDAO);
                    break;
                case DiscoveryType.Image:
                    ProcessImage(crawlRequest, imageManager, arachnodeDAO);
                    break;
                case DiscoveryType.WebPage:
                    if (crawlRequest.Discovery.ExpectFileOrImage || _discoveryManager.IsCrawlRestricted(crawlRequest, crawlRequest.Parent.Uri.AbsoluteUri))
                    {
                        /*<img src="http://msn.com"></a> would return a WebPage.  If the Crawl was restricting crawling to fark.com, and a 
                         * page on fark.com listed the aforementioned Image and we didn't make this check, then the Crawl would create a 
                         * record in the WebPages table, which would not be correct.
                         * A CurrentDepth of '0' means a request was made for a File or an Image.
                         */
                        //ANODET: Improve parsing... check the regular expressions...
                        crawlRequest.Crawl.UnassignedDiscoveries.Remove(crawlRequest.Discovery.Uri.AbsoluteUri);

                        return;
#if !DEMO
                        //throw new Exception("A CrawlRequest was created for a File or an Image, but the HttpWebResponse returned a WebPage.  This is typically indicative of invalid HTML.");
#endif
                    }
                    ProcessWebPage(crawlRequest, webPageManager, arachnodeDAO);
                    break;
#if !DEMO
                case DiscoveryType.None:

                    if (ApplicationSettings.InsertDisallowedAbsoluteUris)
                    {
                        arachnodeDAO.InsertDisallowedAbsoluteUri(crawlRequest.DataType.ContentTypeID, (int)crawlRequest.DataType.DiscoveryType, crawlRequest.Parent.Uri.AbsoluteUri, crawlRequest.Discovery.Uri.AbsoluteUri, "Disallowed by unassigned DataType.  (" + crawlRequest.DataType.ContentType + ")", ApplicationSettings.ClassifyAbsoluteUris);
                    }

                    break;
#endif
            }

            //remove the reference from the crawl.
            crawlRequest.Crawl.UnassignedDiscoveries.Remove(crawlRequest.Discovery.Uri.AbsoluteUri);
        }

        /// <summary>
        /// 	Processes the file or image discovery.
        /// </summary>
        /// <param name = "crawlRequest">The crawl request.</param>
        /// <param name = "fileOrImageDiscovery">The file or image discovery.</param>
        /// <param name = "arachnodeDAO">The arachnode DAO.</param>
        public override void ProcessFileOrImageDiscovery(CrawlRequest<TArachnodeDAO> crawlRequest, Discovery<TArachnodeDAO> fileOrImageDiscovery, IArachnodeDAO arachnodeDAO)
        {
            if (fileOrImageDiscovery.DiscoveryState == DiscoveryState.Undiscovered)
            {
                _cache.AddCrawlRequestToBeCrawled(crawlRequest.Crawl.UncrawledCrawlRequests, new CrawlRequest<TArachnodeDAO>(crawlRequest, fileOrImageDiscovery, crawlRequest.CurrentDepth, crawlRequest.CurrentDepth, crawlRequest.RestrictCrawlTo, crawlRequest.RestrictDiscoveriesTo, crawlRequest.Priority + 1000000 + fileOrImageDiscovery.PriorityBoost, RenderType.None, RenderType.None), false, false, arachnodeDAO, false);
            }
            else
            {
                switch (fileOrImageDiscovery.DiscoveryType)
                {
                    case DiscoveryType.File:
                        if (ApplicationSettings.InsertFileDiscoveries && fileOrImageDiscovery.IsStorable)
                        {
                            arachnodeDAO.InsertFileDiscovery(crawlRequest.Discovery.Uri.AbsoluteUri, fileOrImageDiscovery.Uri.AbsoluteUri);
                        }

                        Counters.GetInstance().FilesDiscovered(1);

                        _consoleManager.OutputCacheHit(crawlRequest.Crawl.CrawlInfo, crawlRequest, fileOrImageDiscovery);
                        break;
                    case DiscoveryType.Image:
                        if (ApplicationSettings.InsertImageDiscoveries && fileOrImageDiscovery.IsStorable)
                        {
                            arachnodeDAO.InsertImageDiscovery(crawlRequest.Discovery.Uri.AbsoluteUri, fileOrImageDiscovery.Uri.AbsoluteUri);
                        }

                        Counters.GetInstance().ImagesDiscovered(1);

                        _consoleManager.OutputCacheHit(crawlRequest.Crawl.CrawlInfo, crawlRequest, fileOrImageDiscovery);
                        break;
                    case DiscoveryType.None:
                        //another thread has discovered it but it hasn't been crawled yet, or, another WebPage has also discovered the Discovery but the Discovery is disallowed.
                        if (!fileOrImageDiscovery.IsDisallowed)
                        {
                            if (!crawlRequest.Crawl.UnassignedDiscoveries.Contains(fileOrImageDiscovery.Uri.AbsoluteUri))
                            {
                                crawlRequest.Crawl.UnassignedDiscoveries.Add(fileOrImageDiscovery.Uri.AbsoluteUri);

                                _cache.AddCrawlRequestToBeCrawled(crawlRequest.Crawl.UncrawledCrawlRequests, new CrawlRequest<TArachnodeDAO>(crawlRequest, fileOrImageDiscovery, crawlRequest.CurrentDepth, crawlRequest.CurrentDepth, crawlRequest.RestrictCrawlTo, crawlRequest.RestrictDiscoveriesTo, int.MaxValue + fileOrImageDiscovery.PriorityBoost, RenderType.None, RenderType.None), false, false, arachnodeDAO, false);
                            }
                            else
                            {
                                //ANODET: Breakpoint...
                            }
                        }
                        else
                        {
                            if (ApplicationSettings.InsertDisallowedAbsoluteUris)
                            {
                                arachnodeDAO.InsertDisallowedAbsoluteUri(0, (int)DiscoveryType.None, crawlRequest.Discovery.Uri.AbsoluteUri, fileOrImageDiscovery.Uri.AbsoluteUri, fileOrImageDiscovery.IsDisallowedReason, ApplicationSettings.ClassifyAbsoluteUris);
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 	Processes the file.
        /// </summary>
        /// <param name = "crawlRequest">The crawl request.</param>
        /// <param name = "fileManager">The file manager.</param>
        /// <param name = "arachnodeDAO">The arachnode DAO.</param>
        protected override void ProcessFile(CrawlRequest<TArachnodeDAO> crawlRequest, FileManager<TArachnodeDAO> fileManager, IArachnodeDAO arachnodeDAO)
        {
            _consoleManager.OutputFileDiscovered(crawlRequest.Crawl.CrawlInfo.ThreadNumber, crawlRequest, crawlRequest.Discovery);
            Counters.GetInstance().FilesDiscovered(1);

            if (crawlRequest.Discovery.DiscoveryState == DiscoveryState.PostRequest)
            {
                fileManager.ManageFile(crawlRequest);
            }
            else
            {
                if (ApplicationSettings.InsertFileDiscoveries)
                {
                    arachnodeDAO.InsertFileDiscovery(crawlRequest.Parent.Uri.AbsoluteUri, crawlRequest.Discovery.Uri.AbsoluteUri);
                }

                _consoleManager.OutputCacheHit(crawlRequest.Crawl.CrawlInfo, crawlRequest, crawlRequest.Discovery);
            }
        }

        /// <summary>
        /// 	Processes the image.
        /// </summary>
        /// <param name = "crawlRequest">The crawl request.</param>
        /// <param name = "imageManager">The image manager.</param>
        /// <param name = "arachnodeDAO">The arachnode DAO.</param>
        protected override void ProcessImage(CrawlRequest<TArachnodeDAO> crawlRequest, ImageManager<TArachnodeDAO> imageManager, IArachnodeDAO arachnodeDAO)
        {
            _consoleManager.OutputImageDiscovered(crawlRequest.Crawl.CrawlInfo.ThreadNumber, crawlRequest, crawlRequest.Discovery);
            Counters.GetInstance().ImagesDiscovered(1);

            if (crawlRequest.Discovery.DiscoveryState == DiscoveryState.PostRequest)
            {
                imageManager.ManageImage(crawlRequest);
            }
            else
            {
                if (ApplicationSettings.InsertImageDiscoveries)
                {
                    arachnodeDAO.InsertImageDiscovery(crawlRequest.Parent.Uri.AbsoluteUri, crawlRequest.Discovery.Uri.AbsoluteUri);
                }

                _consoleManager.OutputCacheHit(crawlRequest.Crawl.CrawlInfo, crawlRequest, crawlRequest.Discovery);
            }
        }

        /// <summary>
        /// 	Processes the web page.
        /// </summary>
        /// <param name = "crawlRequest">The crawl request.</param>
        /// <param name = "webPageManager">The web page manager.</param>
        /// <param name = "arachnodeDAO">The arachnode DAO.</param>
        protected override void ProcessWebPage(CrawlRequest<TArachnodeDAO> crawlRequest, WebPageManager<TArachnodeDAO> webPageManager, IArachnodeDAO arachnodeDAO)
        {
            _consoleManager.OutputWebPageDiscovered(crawlRequest.Crawl.CrawlInfo.ThreadNumber, crawlRequest);
            Counters.GetInstance().WebPagesDiscovered(1);

            /**/

            webPageManager.ManageWebPage(crawlRequest);

            //the Crawler may(/will) be null if PostProcessing...
            if (ApplicationSettings.ProcessDiscoveriesAsynchronously && !crawlRequest.Crawl.IsProcessingDiscoveriesAsynchronously && crawlRequest.Crawl.Crawler != null)
            {
                crawlRequest.Crawl.Crawler.Engine.DiscoveryProcessors[crawlRequest.Crawl.CrawlInfo.ThreadNumber].AddCrawlRequestToBeProcessed(crawlRequest);
            }
            else
            {
                ProcessDiscoveries(crawlRequest, arachnodeDAO);
            }
        }

        public override void ProcessDiscoveries(CrawlRequest<TArachnodeDAO> crawlRequest, IArachnodeDAO arachnodeDAO)
        {
            /**/

            //Email Addresses

            ProcessEmailAddresses(crawlRequest, arachnodeDAO);

            /**/

            //HyperLinks

            ProcessHyperLinks(crawlRequest, arachnodeDAO);

            /**/

            //Files and Images

            ProcessFilesAndImages(crawlRequest, arachnodeDAO);
        }

        /// <summary>
        /// 	Processes the email addresses.
        /// </summary>
        /// <param name = "crawlRequest">The crawl request.</param>
        /// <param name = "arachnodeDAO">The arachnode DAO.</param>
        public override void ProcessEmailAddresses(CrawlRequest<TArachnodeDAO> crawlRequest, IArachnodeDAO arachnodeDAO)
        {
            if (ApplicationSettings.AssignEmailAddressDiscoveries)
            {
                _discoveryManager.AssignEmailAddressDiscoveries(crawlRequest, arachnodeDAO);
            }

            foreach (Discovery<TArachnodeDAO> emailAddressDiscovery in crawlRequest.Discoveries.EmailAddresses.Values)
            {
                if (!emailAddressDiscovery.IsDisallowed)
                {
                    if (emailAddressDiscovery.DiscoveryState == DiscoveryState.Undiscovered)
                    {
                        emailAddressDiscovery.DiscoveryState = DiscoveryState.Discovered;

                        if (ApplicationSettings.InsertEmailAddresses && emailAddressDiscovery.IsStorable)
                        {
                            arachnodeDAO.InsertEmailAddress(crawlRequest.Discovery.Uri.AbsoluteUri, emailAddressDiscovery.Uri.AbsoluteUri, ApplicationSettings.ClassifyAbsoluteUris);
                        }

                        _consoleManager.OutputEmailAddressDiscovered(crawlRequest.Crawl.CrawlInfo.ThreadNumber, crawlRequest, emailAddressDiscovery);
                    }
                    else
                    {
                        if (ApplicationSettings.InsertEmailAddressDiscoveries && emailAddressDiscovery.IsStorable)
                        {
                            arachnodeDAO.InsertEmailAddressDiscovery(crawlRequest.Discovery.Uri.AbsoluteUri, emailAddressDiscovery.Uri.AbsoluteUri);
                        }

                        _consoleManager.OutputCacheHit(crawlRequest.Crawl.CrawlInfo, crawlRequest, emailAddressDiscovery);
                    }
                }
                else
                {
                    if (ApplicationSettings.InsertDisallowedAbsoluteUris)
                    {
                        if (emailAddressDiscovery.DiscoveryState == DiscoveryState.Undiscovered)
                        {
                            arachnodeDAO.InsertDisallowedAbsoluteUri(crawlRequest.DataType.ContentTypeID, (int)crawlRequest.DataType.DiscoveryType, crawlRequest.Discovery.Uri.AbsoluteUri, emailAddressDiscovery.Uri.AbsoluteUri, emailAddressDiscovery.IsDisallowedReason, ApplicationSettings.ClassifyAbsoluteUris);
                        }
                        else
                        {
                            if (ApplicationSettings.InsertDisallowedAbsoluteUriDiscoveries)
                            {
                                arachnodeDAO.InsertDisallowedAbsoluteUriDiscovery(crawlRequest.Discovery.Uri.AbsoluteUri, emailAddressDiscovery.Uri.AbsoluteUri);
                            }
                        }
                    }

                    _consoleManager.OutputIsDisallowedReason(crawlRequest.Crawl.CrawlInfo, crawlRequest, emailAddressDiscovery);
                }
            }

            Counters.GetInstance().EmailAddressesDiscovered(crawlRequest.Discoveries.EmailAddresses.Count);
        }

        /// <summary>
        /// 	Processes the files and images.
        /// </summary>
        /// <param name = "crawlRequest">The crawl request.</param>
        /// <param name = "arachnodeDAO">The arachnode DAO.</param>
        protected override void ProcessFilesAndImages(CrawlRequest<TArachnodeDAO> crawlRequest, IArachnodeDAO arachnodeDAO)
        {
            if (ApplicationSettings.AssignFileAndImageDiscoveries)
            {
                _discoveryManager.AssignFileAndImageDiscoveries(crawlRequest, arachnodeDAO);
            }

            foreach (Discovery<TArachnodeDAO> fileOrImageDiscovery in crawlRequest.Discoveries.FilesAndImages.Values)
            {
                if (!fileOrImageDiscovery.IsDisallowed)
                {
                    ProcessFileOrImageDiscovery(crawlRequest, fileOrImageDiscovery, arachnodeDAO);
                }
            }
        }

        /// <summary>
        /// 	Processes the hyper links.
        /// </summary>
        /// <param name = "crawlRequest">The crawl request.</param>
        /// <param name = "arachnodeDAO">The arachnode DAO.</param>
        public override void ProcessHyperLinks(CrawlRequest<TArachnodeDAO> crawlRequest, IArachnodeDAO arachnodeDAO)
        {
            if (ApplicationSettings.AssignHyperLinkDiscoveries)
            {
                _discoveryManager.AssignHyperLinkDiscoveries(crawlRequest, arachnodeDAO);
            }

            foreach (Discovery<TArachnodeDAO> hyperLinkDiscovery in crawlRequest.Discoveries.HyperLinks.Values)
            {
                if (!hyperLinkDiscovery.IsDisallowed)
                {
                    if (hyperLinkDiscovery.DiscoveryState == DiscoveryState.Undiscovered)
                    {
                        if (crawlRequest.CurrentDepth < crawlRequest.MaximumDepth)
                        {
                            if (!_discoveryManager.IsCrawlRestricted(crawlRequest, hyperLinkDiscovery.Uri.AbsoluteUri))
                            {
                                _cache.AddCrawlRequestToBeCrawled(new CrawlRequest<TArachnodeDAO>(crawlRequest, hyperLinkDiscovery, crawlRequest.CurrentDepth + 1, crawlRequest.MaximumDepth, crawlRequest.RestrictCrawlTo, crawlRequest.RestrictDiscoveriesTo, crawlRequest.Priority + hyperLinkDiscovery.PriorityBoost, crawlRequest.RenderTypeForChildren, crawlRequest.RenderTypeForChildren), false, false, arachnodeDAO);
                            }
                        }

                        if (ApplicationSettings.InsertHyperLinks && hyperLinkDiscovery.IsStorable)
                        {
                            arachnodeDAO.InsertHyperLink(crawlRequest.Discovery.Uri.AbsoluteUri, hyperLinkDiscovery.Uri.AbsoluteUri, ApplicationSettings.ClassifyAbsoluteUris);
                        }

                        _consoleManager.OutputHyperLinkDiscovered(crawlRequest.Crawl.CrawlInfo.ThreadNumber, crawlRequest, hyperLinkDiscovery);
                    }
                    else
                    {
                        if (ApplicationSettings.InsertHyperLinkDiscoveries && hyperLinkDiscovery.IsStorable)
                        {
                            arachnodeDAO.InsertHyperLinkDiscovery(crawlRequest.Discovery.Uri.AbsoluteUri, hyperLinkDiscovery.Uri.AbsoluteUri);
                        }

                        _consoleManager.OutputCacheHit(crawlRequest.Crawl.CrawlInfo, crawlRequest, hyperLinkDiscovery);
                    }
                }
                else
                {
                    if (ApplicationSettings.InsertDisallowedAbsoluteUris)
                    {
                        if (hyperLinkDiscovery.DiscoveryState == DiscoveryState.Undiscovered)
                        {
                            arachnodeDAO.InsertDisallowedAbsoluteUri(crawlRequest.DataType.ContentTypeID, (int)crawlRequest.DataType.DiscoveryType, crawlRequest.Discovery.Uri.AbsoluteUri, hyperLinkDiscovery.Uri.AbsoluteUri, hyperLinkDiscovery.IsDisallowedReason, ApplicationSettings.ClassifyAbsoluteUris);
                        }
                        else
                        {
                            if (ApplicationSettings.InsertDisallowedAbsoluteUriDiscoveries)
                            {
                                arachnodeDAO.InsertDisallowedAbsoluteUriDiscovery(crawlRequest.Discovery.Uri.AbsoluteUri, hyperLinkDiscovery.Uri.AbsoluteUri);
                            }
                        }
                    }

                    _consoleManager.OutputIsDisallowedReason(crawlRequest.Crawl.CrawlInfo, crawlRequest, hyperLinkDiscovery);
                }
            }

            Counters.GetInstance().HyperLinksDiscovered(crawlRequest.Discoveries.HyperLinks.Count);
        }
    }
}