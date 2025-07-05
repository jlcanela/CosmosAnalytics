Certainly! Here is a **TypeScript type representation** of your Entity Search API specification, designed to be readable, expressive, and directly mappable to your requirements.

```typescript
// Supported operations for each field type
type StringOperation = 'equals' | 'contains' | 'startsWith' | 'endsWith';
type EnumOperation = 'equals' | 'contains';
type NumberOperation = 'equals' | 'greaterThan' | 'greaterEqualThan' | 'lessThan' | 'lessEqualThan';
type NumberRangeOperation = 'between';
type DateOperation = 'before' | 'after' ;
type DateRangeOperation = 'between';
type UniversalOperation = 'exists' | 'notExists';

// Search parameter for string, enum, number, or date fields
type SearchParameter =
  | { field: string; operation: StringOperation; value: string }
  | { field: string; operation: EnumOperation; value: string | string[] }
  | { field: string; operation: NumberOperation; value: number }
  | { field: string; operation: NumberRangeOperation; value: [number, number] }
  | { field: string; operation: DateOperation; value: string } // ISO date strings
  | { field: string; operation: DateRangeOperation; value: [string, string] } // ISO date strings 
  | { field: string; operation: UniversalOperation };

// Sorting
type SortOrder = 'asc' | 'desc';

interface SortField {
  field: string;
  order?: SortOrder; // Optional, default is implementation-defined
}

// Main search request
interface EntitySearchRequest {
  type: string; // e.g., "user", "project"
  searchParameters?: SearchParameter[];
  pageSize?: number;
  continuationToken?: string;
  sort?: SortField[];
  fields?: string[];
  count?: boolean; // If true, include total count in the response
  facets?: string[]; // Fields (usually enum/string) to facet on
  // Optional: for future versioning, etc.
  version?: string;
}

// Example response interfaces

interface FacetResult {
  field: string;
  counts: Record; // e.g., { "active": 12, "pending": 5 }
}

interface EntitySearchResponse {
  results: T[];
  totalCount?: number;
  continuationToken?: string;
  facets?: FacetResult[];
  // For error reporting
  error?: string;
}

// Example error response (alternative form)
interface ErrorResponse {
  error: string;
}

// Example usage
const exampleRequest: EntitySearchRequest = {
  type: "project",
  searchParameters: [
    { field: "name", operation: "contains", value: "alpha" },
    { field: "status", operation: "equals", value: ["active", "pending"] },
    { field: "createdAt", operation: "after", value: "2024-01-01" }
  ],
  pageSize: 20,
  continuationToken: "abc123",
  sort: [{ field: "createdAt", order: "desc" }],
  fields: ["id", "name", "status", "createdAt"],
  count: true,
  facets: ["status"]
};
```

## Notes

- This representation is **extensible** (you can add more operations or types easily).
- It is **type-safe**: TypeScript will help you catch invalid combinations.
- You can use this as a basis for both **frontend and backend validation** or documentation.
- You can add additional properties (e.g., version, security tokens) as needed.

**Let me know if you want a version with advanced logical operators (AND/OR/NOT), or if you need a sample response for a specific entity!**