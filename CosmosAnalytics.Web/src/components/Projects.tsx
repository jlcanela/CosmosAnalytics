import { useState, useCallback, useEffect } from "react";
import {
  Table, Loader, Alert, Text, Button, Group, TextInput, MultiSelect, Collapse, Chip, Paper, Stack
} from "@mantine/core";
import { IconFilter } from "@tabler/icons-react";
import createFetchClient from "openapi-fetch";
import createClient from "openapi-react-query";
import type { paths } from "../schema-api";

const fetchClient = createFetchClient<paths>({
  baseUrl: "https://localhost:7415",
});
const $api = createClient(fetchClient);

const PROJECT_STATUSES = [
  { value: "Active", label: "Active" },
  { value: "Cancelled", label: "Cancelled" },
  { value: "Completed", label: "Completed" },
  { value: "OnHold", label: "On Hold" },
  { value: "Planned", label: "Planned" }
];

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

  // UI state for filters
  const [filterPanelOpen, setFilterPanelOpen] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [status, setStatus] = useState<string[]>([]);
  const [activeFilters, setActiveFilters] = useState<
    { key: string; label: string; value: string | string[] }[]
  >([]);

  // Compose the search request object for the API
  const searchParameters: any[] = [];
  activeFilters.forEach((filter) => {
    if (filter.key === "name") {
      searchParameters.push({
        type: "string",
        field: "name",
        operation: "contains",
        value: filter.value
      });
    } else if (filter.key === "description") {
      searchParameters.push({
        type: "string",
        field: "description",
        operation: "contains",
        value: filter.value
      });
    } else if (filter.key === "status" && Array.isArray(filter.value) && filter.value.length > 0) {
      searchParameters.push({
        type: "enum",
        field: "status",
        operation: "equals",
        value: filter.value
      });
    }
  });

  const searchRequest: Partial<paths["/api/search"]["post"]["requestBody"]["content"]["application/json"]> = {
    type: "project",
    searchParameters: searchParameters.length ? searchParameters : undefined
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

  // Handle filter apply
  const handleApplyFilters = () => {
    const newFilters: typeof activeFilters = [];
    if (name.trim()) {
      newFilters.push({ key: "name", label: `Name: ${name.trim()}`, value: name.trim() });
    }
    if (description.trim()) {
      newFilters.push({ key: "description", label: `Description: ${description.trim()}`, value: description.trim() });
    }
    if (status.length > 0) {
      newFilters.push({ key: "status", label: `Status: ${status.join(", ")}`, value: [...status] });
    }
    setActiveFilters(newFilters);
    setTokens([null]);
    setCurrentPage(0);
    setFilterPanelOpen(false);
  };

  // Remove a filter chip
  const handleRemoveFilter = (index: number) => {
    const newFilters = [...activeFilters];
    newFilters.splice(index, 1);
    setActiveFilters(newFilters);
    setTokens([null]);
    setCurrentPage(0);
  };

  return (
    <div>
      <h1>Projects</h1>
      <Group mb="md">
        <Button
          leftSection={<IconFilter size={16} />}
          variant={filterPanelOpen ? "filled" : "outline"}
          onClick={() => setFilterPanelOpen((v) => !v)}
        >
          {filterPanelOpen ? "Hide Filters" : "Show Filters"}
        </Button>
        {activeFilters.length > 0 && (
          <Group>
            {activeFilters.map((filter, idx) => (
              <Chip
                key={filter.key + filter.label}
                checked
                onChange={() => handleRemoveFilter(idx)}
                color="blue"
                variant="filled"
              >
                {filter.label}
              </Chip>
            ))}
          </Group>
        )}
      </Group>
      <Collapse in={filterPanelOpen}>
        <Paper withBorder shadow="xs" p="md" mb="md">
          <Stack gap="md">
            <TextInput
              label="Name contains"
              placeholder="e.g. Acme"
              value={name}
              onChange={(e) => setName(e.currentTarget.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") handleApplyFilters();
              }}
            />
            <TextInput
              label="Description contains"
              placeholder="e.g. SaaS"
              value={description}
              onChange={(e) => setDescription(e.currentTarget.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") handleApplyFilters();
              }}
            />
            <MultiSelect
              label="Status"
              data={PROJECT_STATUSES}
              value={status}
              onChange={setStatus}
              placeholder="Select status"
              clearable
            />
            <Button onClick={handleApplyFilters}>Apply Filters</Button>
          </Stack>
        </Paper>
      </Collapse>
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
