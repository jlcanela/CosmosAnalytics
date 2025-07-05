import { useEffect, useState } from "react";

// Utility hook: fetches the Swagger spec from a URL
export function useSwaggerSpec(url: string) {
  const [swaggerSpec, setSwaggerSpec] = useState<any>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    async function fetchSwagger() {
      try {
        const response = await fetch(url);
        if (!response.ok) throw new Error(`Failed to fetch: ${response.status}`);
        const spec = await response.json();
        setSwaggerSpec(spec);
      } catch (err) {
        setError(err as Error);
      }
    }
    fetchSwagger();
  }, [url]);

  return { swaggerSpec, error };
}

// Type for each searchable field's info
export type SearchableFieldInfo = {
  name: string;
  type: string;
  enum?: string[];
  nullable?: boolean;
  [key: string]: any; // allow additional OpenAPI properties if needed
};

// Custom hook: returns detailed info for all searchable fields of a given type
export function useSearchableFields(
  url: string,
  typeName: string
): { fields: SearchableFieldInfo[]; loading: boolean; error: Error | null } {
  const { swaggerSpec, error } = useSwaggerSpec(url);
  const [fields, setFields] = useState<SearchableFieldInfo[]>([]);

  useEffect(() => {
    if (!swaggerSpec) return;

    // Find the schema for the given type
    const schema = swaggerSpec?.components?.schemas?.[typeName];
    if (!schema || !schema.properties) {
      setFields([]);
      return;
    }

    // Extract detailed info for properties with x-searchable: true
    const searchable: SearchableFieldInfo[] = Object.entries(schema.properties)
      .filter(([_, prop]: [string, any]) => prop["x-searchable"])
      .map(([name, prop]: [string, any]) => ({
        name,
        type: prop.type,
        enum: prop.enum,
        nullable: prop.nullable,
        // Spread any other custom OpenAPI properties if desired
        ...prop
      }));

    setFields(searchable);
  }, [swaggerSpec, typeName]);

  return { fields, loading: !swaggerSpec && !error, error };
}

// Example usage in a component
/*
function Example() {
  const { fields, loading, error } = useSearchableFields(
    "https://localhost:7415/swagger/v1/swagger.json",
    "Project"
  );

  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {error.message}</div>;

  return (
    <div>
      <h3>Searchable fields for Project:</h3>
      <ul>
        {fields.map((f) => (
          <li key={f.name}>
            <b>{f.name}</b> ({f.type})
            {f.enum && (
              <> — enum: [{f.enum.join(", ")}]</>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
*/
