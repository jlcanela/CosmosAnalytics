import { useState, useCallback, useEffect } from "react";
import {
    Table, Loader, Alert, Text, Button, Group
} from "@mantine/core";
import createFetchClient from "openapi-fetch";
import createClient from "openapi-react-query";
import type { paths } from "../schema-api";
import { useSearchableFields } from "@/hooks/swagger-hooks";
import { DynamicFilterPanel } from "./FilterPanel";

// TODO: use https://v2.mantine-react-table.com/docs/examples/react-query as template

const fetchClient = createFetchClient<paths>({
    baseUrl: "https://localhost:7415",
});
const $api = createClient(fetchClient);

function ProjectTable({
    projects,
}: {
    projects: NonNullable<paths["/api/search"]["post"]["responses"]["200"]["content"]["application/json"]>["items"];
}) {
    if (!projects || projects.length === 0) {
        return <Text>No projects found.</Text>;
    }
    return (
        <Table striped highlightOnHover withTableBorder>
            <Table.Thead>
                <Table.Tr>
                    <Table.Th>ID</Table.Th>
                    <Table.Th>Name</Table.Th>
                    <Table.Th>Description</Table.Th>
                    <Table.Th>Status</Table.Th>
                </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
                {projects.map((project) => (
                    <Table.Tr key={project.id ?? "-"}>
                        <Table.Td>{project.id ?? "—"}</Table.Td>
                        <Table.Td>{project.name ?? "—"}</Table.Td>
                        <Table.Td>{project.description ?? "—"}</Table.Td>
                        <Table.Td>{project.status ?? "—"}</Table.Td>
                    </Table.Tr>
                ))}
            </Table.Tbody>
        </Table>
    );
}

function ProjectsLoader({
    searchRequest,
    continuationToken,
    pageSize = 10,
    onNewToken
}: {
    searchRequest: Partial<paths["/api/search"]["post"]["requestBody"]["content"]["application/json"]>;
    continuationToken: string | null;
    pageSize?: number;
    onNewToken: (token: string | null) => void
}) {
    const body = {
        ...searchRequest,
        pageSize,
        continuationToken: continuationToken ?? undefined,
    };

    const { data, error, isLoading } = $api.useQuery(
        "post",
        "/api/search",
        { body }
    );

    useEffect(() => {
        if (data?.continuationToken) {
            onNewToken(data.continuationToken);
        }
    }, [data?.continuationToken, onNewToken]);

    if (isLoading) return <Loader />;
    if (error) return <Alert color="red">{`An error occurred: ${error}`}</Alert>;
    if (!data) return <Text>No data received.</Text>;

    return <ProjectTable projects={data.items} />;
}

export function Projects() {
    const [tokens, setTokens] = useState<(string | null)[]>([null]);
    const [currentPage, setCurrentPage] = useState(0);
    const [searchRequest, setSearchRequest] = useState<Partial<paths["/api/search"]["post"]["requestBody"]["content"]["application/json"]>>({
        type: "project",
        searchParameters: [],
    });

    const { fields: searchableFields, loading, error } = useSearchableFields(
        "https://localhost:7415/swagger/v1/swagger.json",
        "Project"
    );
    const buildSearchParameters = (filterState: { [field: string]: any }) => {
        const params: any[] = [];
        Object.entries(filterState).forEach(([field, value]) => {
            const fieldInfo = searchableFields.find((f) => f.name === field);
            if (!fieldInfo) return;
            if (fieldInfo.enum) {
                // Enum: one searchParameter with array value
                if (Array.isArray(value) && value.length > 0) {
                    params.push({
                        type: "enum",
                        field,
                        operation: "equals",
                        value,
                    });
                }
            } else if (fieldInfo.type === "string") {
                // String: handle both array and string
                if (Array.isArray(value)) {
                    value.forEach((val: string) => {
                        if (val && val !== "") {
                            params.push({
                                type: "string",
                                field,
                                operation: "contains",
                                value: val,
                            });
                        }
                    });
                } else if (typeof value === "string" && value !== "") {
                    params.push({
                        type: "string",
                        field,
                        operation: "contains",
                        value,
                    });
                }
            }
            // Add more types as needed
        });
        return params;
    };


    useEffect(() => {
        console.log("searchRequest", searchRequest)
    });

    // Compose searchParameters for the API from filterState
    const handleChangeFilters = (filterState: { [field: string]: any }) => {

        const searchParameters = buildSearchParameters(filterState);

        setSearchRequest({
            type: "project",
            searchParameters
        });
        setTokens([null]);
        setCurrentPage(0);
    };

    // Pagination logic
    const onNewToken = useCallback(
        (newToken: string | null) => {
            setTokens((prevTokens) => {
                if (!newToken || prevTokens.includes(newToken)) return prevTokens;
                return [...prevTokens, newToken];
            });
        },
        []
    );

    const goToNextPage = () => {
        if (currentPage < tokens.length - 1) {
            setCurrentPage(currentPage + 1);
        }
    };

    const goToPreviousPage = () => {
        if (currentPage > 0) {
            setCurrentPage(currentPage - 1);
        }
    };

    if (loading) return <div>Loading...</div>;
    if (error) return <div>Error: {error.message}</div>;

    return (
        <div>
            <h1>Projects</h1>

            <DynamicFilterPanel
                fields={searchableFields}
                onApply={handleChangeFilters}
            />

            <ProjectsLoader
                searchRequest={searchRequest}
                continuationToken={tokens[currentPage]}
                onNewToken={onNewToken}
            />
            <Group mt="md">
                <Button onClick={goToPreviousPage} disabled={currentPage === 0}>
                    Previous Page
                </Button>
                <Text>
                    Page <b>{currentPage + 1}</b>
                </Text>
                <Button
                    onClick={goToNextPage}
                    disabled={currentPage >= tokens.length - 1}
                >
                    Next Page
                </Button>
            </Group>
        </div>
    );
}
