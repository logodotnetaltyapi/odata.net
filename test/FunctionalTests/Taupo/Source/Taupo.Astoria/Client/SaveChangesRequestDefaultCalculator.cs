﻿//---------------------------------------------------------------------
// <copyright file="SaveChangesRequestDefaultCalculator.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.Test.Taupo.Astoria.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Test.Taupo.Astoria.Common;
    using Microsoft.Test.Taupo.Astoria.Contracts;
    using Microsoft.Test.Taupo.Astoria.Contracts.Client;
    using Microsoft.Test.Taupo.Astoria.Contracts.Http;
    using Microsoft.Test.Taupo.Astoria.Contracts.OData;
    using Microsoft.Test.Taupo.Astoria.Contracts.Product;
    using Microsoft.Test.Taupo.Common;
    using DSClient = Microsoft.OData.Client;

    /// <summary>
    /// The default calculator for the expected requests from DataServiceContext.SaveChanges.
    /// Should be used for positive cases: i.e. assumes there are no errors in the response.
    /// </summary>
    [ImplementationName(typeof(ISaveChangesRequestCalculator), "Default", HelpText = "The default calculator for the expected requests from DataServiceContext.SaveChanges. Should be used for positive cases: i.e. assumes there are no errors in the response.")]
    public class SaveChangesRequestDefaultCalculator : ISaveChangesRequestCalculator
    {
        /// <summary>
        /// The names of the headers that can be generated by this component. Each expected request will either have a specific value
        /// expected for the header, or it will be black-listed (ie, null)
        /// </summary>
        private static readonly string[] headersThatWillBeGenerated = new string[] 
        {
            HttpHeaders.DataServiceVersion, 
            HttpHeaders.MaxDataServiceVersion,
            HttpHeaders.Prefer, 
            HttpHeaders.IfMatch,
            HttpHeaders.HttpMethod 
        };

        /// <summary>
        /// Gets or sets the entity descriptor value calculator
        /// </summary>
        [InjectDependency(IsRequired = true)]
        public IEntityDescriptorValueCalculator EntityDescriptorValueCalculator { get; set; }

        /// <summary>
        /// Gets or sets the xml to payload element converter
        /// </summary>
        [InjectDependency(IsRequired = true)]
        public IXmlToPayloadElementConverter XmlToPayloadElementConverter { get; set; }

        /// <summary>
        /// Gets or sets the Entity Descriptor version calculator
        /// </summary>
        [InjectDependency(IsRequired = true)]
        public IEntityDescriptorVersionCalculator EntityDescriptorVersionCalculator { get; set; }

        /// <summary>
        /// Calculates expected data for the requests from DataServiceContext.SaveChanges.
        /// </summary>
        /// <param name="dataBeforeSaveChanges">The data before save changes.</param>
        /// <param name="context">The DataServiceContext instance which is calling SaveChanges.</param>
        /// <param name="options">The options for saving chnages.</param>
        /// <param name="cachedOperationsFromResponse">The individual operation responses from the response, pre-enumerated and cached.</param>
        /// <returns>The expected set of requests for the call to SaveChanges.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "This class is effectively obsolete, and isn't worth refactoring"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "This class is effectively obsolete, and isn't worth refactoring")]
        public IEnumerable<HttpRequestData> CalculateSaveChangesRequestData(DataServiceContextData dataBeforeSaveChanges, DSClient.DataServiceContext context, SaveChangesOptions options, IEnumerable<DSClient.OperationResponse> cachedOperationsFromResponse)
        {
            ExceptionUtilities.CheckArgumentNotNull(dataBeforeSaveChanges, "dataBeforeSaveChanges");
            ExceptionUtilities.CheckArgumentNotNull(context, "context");
            ExceptionUtilities.CheckArgumentNotNull(cachedOperationsFromResponse, "cachedOperationsFromResponse");

            // make a clone to avoid changing what was passed in
            var contextDataClone = dataBeforeSaveChanges.Clone();

            if (options == SaveChangesOptions.Batch)
            {
                return new[] { BuildBatchRequest(contextDataClone) };
            }

            var responseQueue = new Queue<DSClient.OperationResponse>(cachedOperationsFromResponse);

            var requests = new List<HttpRequestData>();
            foreach (var descriptorData in contextDataClone.GetOrderedChanges())
            {
                var linkDescriptorData = descriptorData as LinkDescriptorData;
                if (linkDescriptorData != null)
                {
                    if (!linkDescriptorData.WillTriggerSeparateRequest())
                    {
                        continue;
                    }

                    var info = linkDescriptorData.SourceDescriptor.LinkInfos.SingleOrDefault(l => l.Name == linkDescriptorData.SourcePropertyName);
                    if (linkDescriptorData.State == EntityStates.Added)
                    {
                        requests.Add(this.CreateAddLinkRequest(linkDescriptorData, info));
                        SetExpectedIfMatchHeader(requests.Last(), null);
                        SetUnexpectedPreferHeader(requests.Last());
                        SetExpectedXHTTPMethodHeader(requests.Last(), false);
                        SetExpectedDataServiceVersion(requests.Last(), DataServiceProtocolVersion.V4);

                        ExceptionUtilities.Assert(responseQueue.Count > 0, "Link operation did not have a response");
                        responseQueue.Dequeue();
                    }
                    else if (linkDescriptorData.State == EntityStates.Modified)
                    {
                        requests.Add(this.CreateSetLinkRequest(linkDescriptorData, info));
                        SetExpectedIfMatchHeader(requests.Last(), null);
                        SetUnexpectedPreferHeader(requests.Last());
                        SetExpectedXHTTPMethodHeader(requests.Last(), context.UsePostTunneling);
                        SetExpectedDataServiceVersion(requests.Last(), DataServiceProtocolVersion.V4);

                        ExceptionUtilities.Assert(responseQueue.Count > 0, "Link operation did not have a response");
                        responseQueue.Dequeue();
                    }
                    else if (linkDescriptorData.State == EntityStates.Deleted)
                    {
                        requests.Add(this.CreateDeleteLinkRequest(linkDescriptorData, info));
                        SetExpectedIfMatchHeader(requests.Last(), null);
                        SetUnexpectedPreferHeader(requests.Last());
                        SetExpectedXHTTPMethodHeader(requests.Last(), context.UsePostTunneling);
                        SetExpectedDataServiceVersion(requests.Last(), DataServiceProtocolVersion.V4);

                        ExceptionUtilities.Assert(responseQueue.Count > 0, "Link operation did not have a response");
                        responseQueue.Dequeue();
                    }
                }

                var entityDescriptorData = descriptorData as EntityDescriptorData;
                if (entityDescriptorData != null)
                {
                    if (entityDescriptorData.State == EntityStates.Added)
                    {
                        requests.Add(this.CreateInsertRequest(contextDataClone, entityDescriptorData));
                        SetExpectedIfMatchHeader(requests.Last(), null);
                        SetExpectedPreferHeader(requests.Last(), context);
                        SetExpectedXHTTPMethodHeader(requests.Last(), false);

                        if (entityDescriptorData.IsMediaLinkEntry)
                        {
                            SetExpectedDataServiceVersion(requests.Last(), DataServiceProtocolVersion.V4);
                        }
                        else if (dataBeforeSaveChanges.AddAndUpdateResponsePreference != DataServiceResponsePreference.None)
                        {
                            SetExpectedDataServiceVersion(requests.Last(), DataServiceProtocolVersion.V4);
                        }
                        else
                        {
                            SetExpectedDataServiceVersion(requests.Last(), this.EntityDescriptorVersionCalculator.CalculateDataServiceVersion(entityDescriptorData, contextDataClone.MaxProtocolVersion));
                        }

                        SetExpectedMaxDataServiceVersion(requests.Last(), context);

                        this.ApplyNextResponseHeadersAndPayload(contextDataClone, entityDescriptorData, responseQueue);

                        if (entityDescriptorData.IsMediaLinkEntry)
                        {
                            requests.Add(this.CreateUpdateRequest(options, entityDescriptorData));
                            SetExpectedIfMatchHeader(requests.Last(), entityDescriptorData.ETag);
                            SetExpectedPreferHeader(requests.Last(), context);
                            SetExpectedXHTTPMethodHeader(requests.Last(), context.UsePostTunneling);
                            SetExpectedDataServiceVersion(requests.Last(), DataServiceProtocolVersion.V4);
                            SetExpectedMaxDataServiceVersion(requests.Last(), context);

                            this.ApplyNextResponseHeadersAndPayload(contextDataClone, entityDescriptorData, responseQueue);
                        }
                    }
                    else
                    {
                        if (entityDescriptorData.IsMediaLinkEntry && entityDescriptorData.DefaultStreamState == EntityStates.Modified)
                        {
                            requests.Add(new HttpRequestData() { Verb = HttpVerb.Put, Uri = entityDescriptorData.EditStreamUri });
                            SetExpectedIfMatchHeader(requests.Last(), entityDescriptorData.StreamETag);
                            SetUnexpectedPreferHeader(requests.Last());
                            SetExpectedXHTTPMethodHeader(requests.Last(), context.UsePostTunneling);
                            SetExpectedDataServiceVersion(requests.Last(), DataServiceProtocolVersion.V4);

                            ExceptionUtilities.Assert(responseQueue.Count > 0, "Stream operation did not have a response");
                            var response = responseQueue.Dequeue();
                            entityDescriptorData.DefaultStreamDescriptor.UpdateFromHeaders(response.Headers);
                        }

                        if (entityDescriptorData.State == EntityStates.Modified)
                        {
                            requests.Add(this.CreateUpdateRequest(options, entityDescriptorData));
                            SetExpectedIfMatchHeader(requests.Last(), entityDescriptorData.ETag);
                            SetExpectedPreferHeader(requests.Last(), context);
                            SetExpectedXHTTPMethodHeader(requests.Last(), context.UsePostTunneling);
                            SetExpectedDataServiceVersion(requests.Last(), this.EntityDescriptorVersionCalculator.CalculateDataServiceVersion(entityDescriptorData, dataBeforeSaveChanges.MaxProtocolVersion));

                            if (options == SaveChangesOptions.PatchOnUpdate || contextDataClone.AddAndUpdateResponsePreference != DataServiceResponsePreference.None)
                            {
                                SetExpectedDataServiceVersion(requests.Last(), DataServiceProtocolVersion.V4);
                            }

                            this.ApplyNextResponseHeadersAndPayload(contextDataClone, entityDescriptorData, responseQueue);
                        }
                        else if (entityDescriptorData.State == EntityStates.Deleted)
                        {
                            requests.Add(this.CreateDeleteRequest(entityDescriptorData));
                            SetExpectedIfMatchHeader(requests.Last(), entityDescriptorData.ETag);
                            SetUnexpectedPreferHeader(requests.Last());
                            SetExpectedXHTTPMethodHeader(requests.Last(), context.UsePostTunneling);
                            SetExpectedDataServiceVersion(requests.Last(), DataServiceProtocolVersion.V4);

                            this.ApplyNextResponseHeadersAndPayload(contextDataClone, entityDescriptorData, responseQueue);
                        }
                    }
                }

                var streamDescriptorData = descriptorData as StreamDescriptorData;
                if (streamDescriptorData != null)
                {
                    if (streamDescriptorData.State == EntityStates.Modified)
                    {
                        requests.Add(new HttpRequestData() { Verb = HttpVerb.Put, Uri = streamDescriptorData.EditLink });
                        SetExpectedIfMatchHeader(requests.Last(), streamDescriptorData.ETag);
                        SetUnexpectedPreferHeader(requests.Last());
                        SetExpectedXHTTPMethodHeader(requests.Last(), context.UsePostTunneling);
                        SetExpectedDataServiceVersion(requests.Last(), DataServiceProtocolVersion.V4);

                        ExceptionUtilities.Assert(responseQueue.Count > 0, "Stream operation did not have a response");
                        var response = responseQueue.Dequeue();
                        streamDescriptorData.UpdateFromHeaders(response.Headers);
                    }
                }
            }

            requests.ForEach(r => SetExpectedMaxDataServiceVersion(r, context));

            ExceptionUtilities.Assert(responseQueue.Count == 0, "Not all responses were applied");
            ExceptionUtilities.Assert(requests.All(r => headersThatWillBeGenerated.All(h => r.Headers.ContainsKey(h))), "Not all requests had all expected headers");

            return requests;
        }

        private static HttpRequestData BuildBatchRequest(DataServiceContextData contextDataClone)
        {
            ExceptionUtilities.CheckObjectNotNull(contextDataClone.BaseUri, "Base uri cannot be null in batch cases");
            var batchRequest = new HttpRequestData()
            {
                Verb = HttpVerb.Post,
                Uri = new Uri(UriHelpers.ConcatenateUriSegments(contextDataClone.BaseUri.OriginalString, Endpoints.Batch))
            };

            SetExpectedIfMatchHeader(batchRequest, null);
            SetUnexpectedPreferHeader(batchRequest);
            SetExpectedXHTTPMethodHeader(batchRequest, false);
            return batchRequest;
        }

        private static void SetExpectedDataServiceVersion(HttpRequestData request, DataServiceProtocolVersion maxDataServiceVersion)
        {
            request.Headers[HttpHeaders.DataServiceVersion] = maxDataServiceVersion.ConvertToHeaderFormat() + ";" + HttpHeaders.NetFx;
        }

        private static void SetExpectedMaxDataServiceVersion(HttpRequestData request, DataServiceProtocolVersion maxDataServiceVersion)
        {
            request.Headers[HttpHeaders.MaxDataServiceVersion] = maxDataServiceVersion.ConvertToHeaderFormat() + ";" + HttpHeaders.NetFx;
        }

        private static void SetExpectedMaxDataServiceVersion(HttpRequestData request, DSClient.DataServiceContext context)
        {
            DataServiceProtocolVersion maxDataServiceVersion = context.MaxProtocolVersion.ToTestEnum();
            SetExpectedMaxDataServiceVersion(request, maxDataServiceVersion); 
        }

        private static void SetExpectedIfMatchHeader(HttpRequestData request, string etag)
        {
            // NOTE: we intentionally set this to null if the etag is null, because null means it was not included
            request.Headers[HttpHeaders.IfMatch] = etag;
        }

        private static void SetUnexpectedPreferHeader(HttpRequestData request)
        {
            request.Headers[HttpHeaders.Prefer] = null;
        }

        private static void SetExpectedPreferHeader(HttpRequestData request, DSClient.DataServiceContext contextData)
        {
            SetExpectedPreferHeader(request, contextData.AddAndUpdateResponsePreference.ToTestEnum());
        }

        private static void SetExpectedPreferHeader(HttpRequestData request, DataServiceResponsePreference preference)
        {
            if (preference == DataServiceResponsePreference.None)
            {
                SetUnexpectedPreferHeader(request);
            }
            else if (preference == DataServiceResponsePreference.IncludeContent)
            {
                request.Headers[HttpHeaders.Prefer] = HttpHeaders.ReturnContent;
            }
            else
            {
                ExceptionUtilities.Assert(preference == DataServiceResponsePreference.NoContent, "Unexpected preference value");
                request.Headers[HttpHeaders.Prefer] = HttpHeaders.ReturnNoContent;
            }
        }

        private static void SetExpectedXHTTPMethodHeader(HttpRequestData request, bool useTunnelling)
        {
            if (useTunnelling)
            {
                request.Headers[HttpHeaders.HttpMethod] = request.Verb.ToHttpMethod();
                request.Verb = HttpVerb.Post;
            }
            else
            {
                request.Headers[HttpHeaders.HttpMethod] = null;
            }
        }

        private void ApplyNextResponseHeadersAndPayload(DataServiceContextData dataBeforeSaveChanges, EntityDescriptorData entityDescriptorData, Queue<DSClient.OperationResponse> responseQueue)
        {
            ExceptionUtilities.CheckArgumentNotNull(dataBeforeSaveChanges, "dataBeforeSaveChanges");
            ExceptionUtilities.CheckArgumentNotNull(entityDescriptorData, "entityDescriptorData");
            ExceptionUtilities.CheckArgumentNotNull(responseQueue, "responseQueue");

            ExceptionUtilities.Assert(responseQueue.Count > 0, "Response queue unexpectedly empty");

            var responseForRequest = responseQueue.Dequeue();
            entityDescriptorData.UpdateFromHeaders(responseForRequest.Headers);
        }

        private HttpRequestData CreateAddLinkRequest(LinkDescriptorData linkDescriptorData, LinkInfoData info)
        {
            return new HttpRequestData() { Uri = this.GetLinkUri(linkDescriptorData, info), Verb = HttpVerb.Post };
        }

        private Uri GetLinkUri(LinkDescriptorData linkDescriptorData, LinkInfoData info)
        {
            Uri linkUri;
            if (info != null && info.RelationshipLink != null)
            {
                linkUri = info.RelationshipLink;
            }
            else
            {
                ExceptionUtilities.CheckObjectNotNull(linkDescriptorData.SourceDescriptor.EditLink, "Edit link of source descriptor cannot be null");
                linkUri = new Uri(UriHelpers.ConcatenateUriSegments(linkDescriptorData.SourceDescriptor.EditLink.OriginalString, Endpoints.Ref, linkDescriptorData.SourcePropertyName));
            }

            return linkUri;
        }

        private HttpRequestData CreateSetLinkRequest(LinkDescriptorData linkDescriptorData, LinkInfoData info)
        {
            HttpVerb verb;
            if (linkDescriptorData.TargetDescriptor == null)
            {
                verb = HttpVerb.Delete;
            }
            else
            {
                verb = HttpVerb.Put;
            }

            return new HttpRequestData() { Uri = this.GetLinkUri(linkDescriptorData, info), Verb = verb };
        }

        private HttpRequestData CreateDeleteLinkRequest(LinkDescriptorData linkDescriptorData, LinkInfoData info)
        {
            string keyString = this.EntityDescriptorValueCalculator.CalculateEntityKey(linkDescriptorData.TargetDescriptor.Entity);
            return new HttpRequestData() { Uri = new Uri(this.GetLinkUri(linkDescriptorData, info).OriginalString + keyString), Verb = HttpVerb.Delete };
        }

        private HttpRequestData CreateInsertRequest(DataServiceContextData dataBeforeSaveChanges, EntityDescriptorData entityDescriptorData)
        {
            Uri insertUri;
            if (entityDescriptorData.InsertLink != null)
            {
                insertUri = entityDescriptorData.InsertLink;
            }
            else
            {
                ExceptionUtilities.CheckObjectNotNull(entityDescriptorData.ParentForInsert, "Entity descriptor data did not have insert link or parent for insert: {0}", entityDescriptorData);
                ExceptionUtilities.CheckObjectNotNull(entityDescriptorData.ParentPropertyForInsert, "Entity descriptor data did not have insert link or parent property for insert: {0}", entityDescriptorData);

                var parentDescriptor = dataBeforeSaveChanges.GetEntityDescriptorData(entityDescriptorData.ParentForInsert);
                var linkInfo = parentDescriptor.LinkInfos.SingleOrDefault(l => l.Name == entityDescriptorData.ParentPropertyForInsert);
                if (linkInfo != null && linkInfo.NavigationLink != null)
                {
                    insertUri = linkInfo.NavigationLink;
                }
                else
                {
                    insertUri = new Uri(UriHelpers.ConcatenateUriSegments(parentDescriptor.EditLink.OriginalString, entityDescriptorData.ParentPropertyForInsert));
                }
            }

            return new HttpRequestData() { Verb = HttpVerb.Post, Uri = insertUri };
        }

        private HttpRequestData CreateDeleteRequest(EntityDescriptorData entityDescriptorData)
        {
            return new HttpRequestData()
            {
                Verb = HttpVerb.Delete,
                Uri = entityDescriptorData.EditLink,
            };
        }

        private HttpRequestData CreateUpdateRequest(SaveChangesOptions options, EntityDescriptorData entityDescriptorData)
        {
            HttpVerb updateVerb = HttpVerb.Patch;
            if (options == SaveChangesOptions.ReplaceOnUpdate)
            {
                updateVerb = HttpVerb.Put;
            }
            else if (options == SaveChangesOptions.PatchOnUpdate)
            {
                updateVerb = HttpVerb.Patch;
            }

            return new HttpRequestData()
            {
                Verb = updateVerb,
                Uri = entityDescriptorData.EditLink,
            };
        }
    }
}
