namespace AIPractice.Bootstrapper;

public record BootstrapperConfig(
    ModelConfig Model
);

public record ModelConfig(
    int VectorSize
);
