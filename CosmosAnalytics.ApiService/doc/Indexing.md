Use indexing for access control
- all entity data necessary for access control must be indexed
- for example, if you want to check if a user is authorized to access a specific resource, you can use the resource ID as the index
- this can help with performance and reduce the number of database queries needed to check access control
- for example, if you have a table of users and a table of resources, you can use the user ID as the index for the resources table to quickly check if the user has access to a specific resource

Use indexing and FullText search
- we can index a field using a Lucene.Net analyzer and storing the array of tokens 
- search text is transformed as an array of token 
- if intersection(field_tokens, search_tokens) is not empty, then the document is a match
