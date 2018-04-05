﻿//---------------------------------------------------------------------
// <copyright file="ODataSimplifiedOptions.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

using System;

namespace Microsoft.OData
{
    /// <summary>
    /// Options which used to control the behaviour related odata simplified.
    /// </summary>
    public sealed class ODataSimplifiedOptions
    {
        /// <summary>
        /// Constructor of ODataSimplifiedOptions
        /// </summary>
        public ODataSimplifiedOptions() : this(null /*version*/)
        {
        }

        /// <summary>
        /// Constructor of ODataSimplifiedOptions
        /// </summary>
        /// <param name="version">The ODataVersion to create Default Options for.</param>
        public ODataSimplifiedOptions(ODataVersion? version)
        {
            this.EnableParsingKeyAsSegmentUrl = true;
            this.EnableWritingKeyAsSegment = false;
            this.EnableReadingKeyAsSegment = false;

            if (version == null || version < ODataVersion.V401)
            {
                this.EnableReadingODataAnnotationWithoutPrefix = false;
                this.EnableWritingODataAnnotationWithoutPrefix = false;
            }
            else
            {
                this.EnableReadingODataAnnotationWithoutPrefix = true;
                this.EnableWritingODataAnnotationWithoutPrefix = true;
            }
        }

        /// <summary>
        /// True if url parser support parsing path with key as segment, otherwise false. The defualt is true.
        /// </summary>
        public bool EnableParsingKeyAsSegmentUrl { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether the reader should put key values in their own URI segment when automatically building URIs.
        /// If this value is false, automatically-generated URLs will take the form "../EntitySet('KeyValue')/..".
        /// If this value is true, automatically-generated URLs will take the form "../EntitySet/KeyValue/..".
        /// This setting only applies to URLs that are automatically generated by the <see cref="ODataMessageReader" /> and the URLs explicitly provided by the server won't be modified.
        /// </summary>
        public bool EnableReadingKeyAsSegment { get; set; }

        /// <summary>
        /// True if can read reserved annotation name without prefix 'odata.', otherwise false.
        /// The default value is false for OData 4.0 and true for OData 4.01.
        /// The option is applied during deserialization.
        /// </summary>
        public bool EnableReadingODataAnnotationWithoutPrefix { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether the writer should put key values in their own URI segment when automatically building URIs.
        /// If this value is false, automatically-generated URLs will take the form "../EntitySet('KeyValue')/..".
        /// If this value is true, automatically-generated URLs will take the form "../EntitySet/KeyValue/..".
        /// This setting only applies to URLs that are automatically generated by the <see cref="ODataMessageWriter" /> and does not modify URLs explicitly provided by the user.
        /// </summary>
        public bool EnableWritingKeyAsSegment { get; set; }

        /// <summary>
        /// True if write reserved annotation name without prefix 'odata.', otherwise false.
        /// The default value is false for OData 4.0, true for OData 4.01.
        /// The option is applied during serialization.
        /// </summary>
        public bool EnableWritingODataAnnotationWithoutPrefix { get; set; }

        /// <summary>
        /// Creates a shallow copy of this <see cref="ODataSimplifiedOptions"/>.
        /// </summary>
        /// <returns>A shallow copy of this <see cref="ODataSimplifiedOptions"/>.</returns>
        public ODataSimplifiedOptions Clone()
        {
            var copy = new ODataSimplifiedOptions();
            copy.CopyFrom(this);
            return copy;
        }

        /// <summary>
        /// Return the instatnce of ODataSimplifiedOptions from container if it container not null.
        /// Otherwise return the static instance of ODataSimplifiedOptions.
        /// </summary>
        /// <param name="container">Container</param>
        /// <param name="version">OData Version</param>
        /// <returns>Instance of GetODataSimplifiedOptions</returns>
        internal static ODataSimplifiedOptions GetODataSimplifiedOptions(IServiceProvider container, ODataVersion? version = null)
        {
            if (container == null)
            {
                return new ODataSimplifiedOptions(version);
            }

            return container.GetRequiredService<ODataSimplifiedOptions>();
        }

        private void CopyFrom(ODataSimplifiedOptions other)
        {
            ExceptionUtils.CheckArgumentNotNull(other, "other");

            this.EnableParsingKeyAsSegmentUrl = other.EnableParsingKeyAsSegmentUrl;
            this.EnableReadingKeyAsSegment = other.EnableReadingKeyAsSegment;
            this.EnableReadingODataAnnotationWithoutPrefix = other.EnableReadingODataAnnotationWithoutPrefix;
            this.EnableWritingKeyAsSegment = other.EnableWritingKeyAsSegment;
            this.EnableWritingODataAnnotationWithoutPrefix = other.EnableWritingODataAnnotationWithoutPrefix;
        }
    }
}