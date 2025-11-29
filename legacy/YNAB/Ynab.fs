namespace YNAB

type AccountInfo = 
  {
    Id : System.Guid
    Name : string
  }

type BudgetInfo = 
  {
    Id : System.Guid
    Name : string
    Accounts : AccountInfo list
  }

