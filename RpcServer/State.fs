module RpcServer.State

open RpcServer.Service.TestService

type State = {
    TestService: TestServiceClient<State>
}