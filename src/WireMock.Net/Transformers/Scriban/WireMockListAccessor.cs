// Copyright © WireMock.Net

// Modified by Ruxo Zheng, 2024.
using System;
using System.Collections.Generic;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace WireMock.Transformers.Scriban;

internal class WireMockListAccessor : IListAccessor, IObjectAccessor
{
    #region IListAccessor
    public int GetLength(TemplateContext context, SourceSpan span, object target) => throw new InvalidOperationException();
    public void SetValue(TemplateContext context, SourceSpan span, object target, int index, object value) => throw new InvalidOperationException();

    public object GetValue(TemplateContext context, SourceSpan span, object target, int index) => target.ToString()!;

    #endregion

    #region IObjectAccessor
    public int GetMemberCount(TemplateContext context, SourceSpan span, object target) => throw new InvalidOperationException();
    public IEnumerable<string> GetMembers(TemplateContext context, SourceSpan span, object target) => throw new InvalidOperationException();
    public bool HasMember(TemplateContext context, SourceSpan span, object target, string member) => throw new InvalidOperationException();
    public bool TryGetValue(TemplateContext context, SourceSpan span, object target, string member, out object value) => throw new InvalidOperationException();
    public bool TrySetValue(TemplateContext context, SourceSpan span, object target, string member, object value) => throw new InvalidOperationException();
    public bool TryGetItem(TemplateContext context, SourceSpan span, object target, object index, out object value) => throw new InvalidOperationException();
    public bool TrySetItem(TemplateContext context, SourceSpan span, object target, object index, object value) => throw new InvalidOperationException();
    public bool HasIndexer => throw new InvalidOperationException();
    public Type IndexType => throw new InvalidOperationException();
    #endregion
}