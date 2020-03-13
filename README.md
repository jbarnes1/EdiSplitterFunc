# EdiSplitterFunc
EdiSplitterFunc - This is an Azure Function  that is based on the EDI Engine at https://github.com/olmelabs/EdiEngine

The purpose is to SPLIT a single large X12 EDI File into 2 halves.
Output is determined based upon the number of EDI transactions in the original file.

USAGE: 
- Meant to be invoked via HTTP POST Request
- Example: pass JSON payload in the POST BODY:  
  - {"ediFileName":"916386MS210_202001190125_000000001.raw","splitHalf":"1"}

Note the "splitHalf" option: normally, you would call the Function 2x.
passing in the Splif Half of "1" or "2" to generate BOTH halves of the orignal large EDI file.
 
