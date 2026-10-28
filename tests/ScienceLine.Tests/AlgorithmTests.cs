using ScienceLine.Web.Services;
using ScienceLine.Web.Models;
using System.ComponentModel.DataAnnotations;
using Xunit;
namespace ScienceLine.Tests;
public class AlgorithmTests
{
    [Theory]
    [InlineData("a\nb","a\nc\nb")]
    [InlineData("a\nb\nc","b")]
    [InlineData("","текст")]
    [InlineData("x\nx\ny","x\ny\ny")]
    [InlineData("A\r\nB","A\nB")]
    public void DiffCanReconstructBothVersions(string before,string after)
    {
        var diff=TextDiff.Compare(before,after);
        Assert.Equal(before.Replace("\r",""),string.Join("\n",diff.Where(x=>x.Kind!="added").Select(x=>x.Text)));
        Assert.Equal(after.Replace("\r",""),string.Join("\n",diff.Where(x=>x.Kind!="removed").Select(x=>x.Text)));
    }
    [Fact] public void DiffRejectsUnboundedMatrix()=>Assert.Throws<ArgumentException>(()=>TextDiff.Compare(string.Join("\n",Enumerable.Repeat("x",201)),"a"));
    [Theory][InlineData(ArticleStatus.Draft,true)][InlineData(ArticleStatus.ChangesRequested,true)][InlineData(ArticleStatus.UnderReview,false)][InlineData(ArticleStatus.Published,false)]
    public void OnlyEditableStatesPermitChanges(ArticleStatus state,bool allowed)=>Assert.Equal(allowed,EditorialWorkflow.CanEdit(state));
    [Theory][InlineData("javascript:alert(1)")][InlineData("file:///C:/private")][InlineData("https-relative")]
    public void SourceRequiresAbsoluteHttpAddress(string source)
    {
        var f=new ArticleForm {SourceUrl=source};
        Assert.Contains(f.Validate(new ValidationContext(f)),v=>v.MemberNames.Contains(nameof(f.SourceUrl)));
    }
}
