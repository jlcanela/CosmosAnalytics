import { useState, useCallback, useEffect } from "react";
import { Table, Loader, Alert, Text, Button, Group, TextInput } from "@mantine/core";
import { TagsInput } from "@mantine/core";
import createFetchClient from "openapi-fetch";
import createClient from "openapi-react-query";
import type { paths } from "../schema-api";

const fetchClient = createFetchClient<paths>({
    baseUrl: "https://localhost:7415",
});
const $api = createClient(fetchClient);

function ProjectTable({
    projects,
}: {
    projects: NonNullable<
        paths["/api/project-search"]["post"]["responses"]["200"]["content"]["application/json"]
    >["items"];
}) {
    if (!projects.length) {
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
                    <Table.Tr key={project.id ?? Math.random()}>
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
    searchRequest: Partial<paths["/api/project-search"]["post"]["requestBody"]["content"]["application/json"]>;
    continuationToken: string | null;
    pageSize?: number;
    onNewToken: (token: string | null) => void
}) {
    const body = {
        ...searchRequest,
        pageSize,
        continuationToken: continuationToken ?? undefined,
    };

    // Use useQuery with a POST queryFn
    const { data, error, isLoading } = $api.useQuery(
        "post",
        "/api/project-search",
        {
            body
        });

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
    // Pagination state
    const [tokens, setTokens] = useState<(string | null)[]>([null]);
    const [currentPage, setCurrentPage] = useState(0);

    // Filter UI state
    const [nameFilter, setNameFilter] = useState("");
    const [activeFilters, setActiveFilters] = useState<string[]>([]);

    // Compose the search request object for the API
    const searchRequest: Partial<paths["/api/project-search"]["post"]["requestBody"]["content"]["application/json"]> = {};
    const nameTag = activeFilters.find((tag) => tag.startsWith('Name="'));
    if (nameTag) {
        const match = nameTag.match(/^Name="(.+)"$/);
        if (match) {
            searchRequest.name = match[1];
        }
    }

    // Add new continuation token if not already present
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

    // Handle filter button click
    const handleFilter = () => {
        if (nameFilter.trim()) {
            const tag = `Name="${nameFilter.trim()}"`;
            if (!activeFilters.includes(tag)) {
                setActiveFilters((prev) => [...prev, tag]);
                setTokens([null]);
                setCurrentPage(0);
            }
            setNameFilter("");
        }
    };

    // Remove filter when tag is removed
    const handleTagsChange = (tags: string[]) => {
        setActiveFilters(tags);
        setTokens([null]);
        setCurrentPage(0);
    };

    return (
        <div>
            <h1>Projects</h1>
            <Group mb="md">
                <TextInput
                    placeholder="Filter by name"
                    value={nameFilter}
                    onChange={(e) => setNameFilter(e.currentTarget.value)}
                    onKeyDown={(e) => {
                        if (e.key === "Enter") handleFilter();
                    }}
                />
                <Button onClick={handleFilter}>Filter</Button>
            </Group>
            <TagsInput
                value={activeFilters}
                onChange={handleTagsChange}
                readOnly={false}
                clearable
                mb="md"
                placeholder="Active filters will appear here"
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
