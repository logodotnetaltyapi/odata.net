﻿//---------------------------------------------------------------------
// <copyright file="EdmModelBaseTests.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Edm.Vocabularies;
using Xunit;

namespace Microsoft.OData.Edm.Tests.Library
{
    public class EdmModelBaseTests
    {
        #region FindDeclaredOperations Tests
        [Fact]
        public void FindDeclaredOperationsShouldFindFullyQualifiedName()
        {
            var model = new TestEdmModel();
            model.Add(new EdmAction("n", "a", null));
            model.Add(new EdmAction("n", "a2", null));

            var findResults = model.FindDeclaredOperations("n.a").ToList();
            var result = Assert.Single(findResults);
            Assert.Equal("a", result.Name);
        }

        [Fact]
        public void FindDeclaredOperationsShouldFindAllActionsWithSameNameAndDifferentNamespaces()
        {
            var model = new TestEdmModel();
            model.Add(new EdmAction("n", "a", null));
            model.Add(new EdmAction("n2", "a", null));

            var findResults = model.FindDeclaredOperations("a").ToList();
            Assert.Empty(findResults);
        }

        [Fact]
        public void FindDeclaredOperationsShouldFindAllActionsShouldNotFindUnknownActionWithName()
        {
            var model = new TestEdmModel();
            model.Add(new EdmAction("n", "a", null));
            model.Add(new EdmAction("n2", "a", null));

            var findResults = model.FindDeclaredOperations("a2");
            Assert.Empty(findResults);
        }

        [Fact]
        public void FindDeclaredOperationsShouldFindAllActionsShouldNotFindUnknownActionWithFullyQualifiedName()
        {
            var model = new TestEdmModel();
            model.Add(new EdmAction("n", "a", null));
            model.Add(new EdmAction("n2", "a", null));

            var findResults = model.FindDeclaredOperations("n.a2");
            Assert.Empty(findResults);
        }
        #endregion

        internal class TestEdmModel : EdmModelBase
        {
            private List<IEdmSchemaElement> schemaElements;

            public TestEdmModel()
                : base(new List<IEdmModel>(), new EdmDirectValueAnnotationsManager())
            {
                this.schemaElements = new List<IEdmSchemaElement>();
            }

            public void Add(IEdmSchemaElement element)
            {
                this.schemaElements.Add(element);
                this.RegisterElement(element);
            }

            public override IEnumerable<IEdmSchemaElement> SchemaElements
            {
                get { return schemaElements; }
            }

            public override IEnumerable<string> DeclaredNamespaces
            {
                get { return this.SchemaElements.Select(s => s.Namespace).Distinct(); }
            }

            public override IEnumerable<IEdmStructuredType> FindDirectlyDerivedTypes(IEdmStructuredType baseType)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
