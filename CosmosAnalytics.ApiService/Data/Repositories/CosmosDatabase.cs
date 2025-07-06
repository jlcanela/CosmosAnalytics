using Microsoft.Azure.Cosmos;

public class CosmosContainers
{
    private Container _projects;
    private Container _index;

    public CosmosContainers(Container projects, Container index)
    {
        _projects = projects;
        _index = index;
    }

    public Container Project() {
        return _projects;
    }
    public Container Index() {
        return _index;
    }

}