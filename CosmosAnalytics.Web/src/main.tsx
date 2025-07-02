import { StrictMode } from "react";
import ReactDOM from "react-dom/client";
import { RouterProvider, createRouter } from "@tanstack/react-router";
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'

// Import the generated route tree
import { routeTree } from "./routeTree.gen";
import { createTheme, MantineProvider } from "@mantine/core";

const queryClient = new QueryClient();

import "@mantine/core/styles.css";

// Create a new router instance
//const router = createRouter({ routeTree });

const router = createRouter({
  routeTree,
  context: {
    queryClient
  },
})

// Register the router instance for type safety
declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}

const theme = createTheme({})

// Render the app
const rootElement = document.getElementById("root")!;
if (!rootElement.innerHTML) {
  const root = ReactDOM.createRoot(rootElement);
  root.render(
    <StrictMode>
      <MantineProvider theme={theme}>
      <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} context={{queryClient}}/>
       <ReactQueryDevtools initialIsOpen={false} />
      </QueryClientProvider>
      </MantineProvider>
    </StrictMode>
  );
}