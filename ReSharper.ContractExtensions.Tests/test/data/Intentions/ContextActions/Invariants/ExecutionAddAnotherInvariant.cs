using System.Diagnostics.Contracts;

class A
{
  private string _anotherString{caret} = "";
  private string _shouldNotBeNull = "";

  public A()
  {
  }

  [ContractInvariantMethod]
  private void ObjectInvariant()
  {
    Contract.Invariant(_shouldNotBeNull != null);
  }
}