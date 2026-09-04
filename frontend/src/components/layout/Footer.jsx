import { Link } from "react-router-dom";
import { FiInstagram, FiMail } from "react-icons/fi";
import { STORE } from "@/data/store.js";

export default function Footer() {
    return (
        <footer className="mt-24 border-t border-sand bg-linen">
            <div className="shell grid gap-10 py-16 sm:grid-cols-2 lg:grid-cols-4">
                <div className="lg:col-span-2 lg:max-w-sm">
                    <img src="/logo-glorific.png" alt="glorific.art" className="h-8 w-auto" />
                    <p className="mt-4 text-sm leading-relaxed text-ink-soft">
                        {STORE.manifesto}
                    </p>

                    <div className="mt-6 flex items-center gap-4">
                        <a
                            href={STORE.social.instagram}
                            target="_blank"
                            rel="noreferrer"
                            aria-label="Instagram"
                            className="flex h-11 w-11 items-center justify-center border border-sand text-ink-soft transition-colors hover:border-ink hover:text-ink"
                        >
                            <FiInstagram size={17} />
                        </a>
                        <a
                            href={`mailto:${STORE.contact.email}`}
                            aria-label="E-mail"
                            className="flex h-11 w-11 items-center justify-center border border-sand text-ink-soft transition-colors hover:border-ink hover:text-ink"
                        >
                            <FiMail size={17} />
                        </a>
                    </div>
                </div>

                <nav aria-label="Loja">
                    <h2 className="eyebrow">Loja</h2>
                    <ul className="mt-5 flex flex-col gap-3">
                        {STORE.navegacao.map((item) => (
                            <li key={item.to}>
                                <Link
                                    to={item.to}
                                    className="text-sm text-ink-soft transition-colors hover:text-ink"
                                >
                                    {item.label}
                                </Link>
                            </li>
                        ))}
                    </ul>
                </nav>

                <nav aria-label="Institucional">
                    <h2 className="eyebrow">Institucional</h2>
                    <ul className="mt-5 flex flex-col gap-3">
                        {STORE.institucional.map((item) => (
                            <li key={item.to}>
                                <Link
                                    to={item.to}
                                    className="text-sm text-ink-soft transition-colors hover:text-ink"
                                >
                                    {item.label}
                                </Link>
                            </li>
                        ))}
                    </ul>
                </nav>
            </div>

            <div className="border-t border-sand">
                <div className="shell flex flex-col gap-2 py-6 sm:flex-row sm:items-center sm:justify-between">
                    <p className="text-xs text-ink-soft">
                        © {new Date().getFullYear()} {STORE.legalName}
                    </p>
                    <p className="text-xs text-taupe">{STORE.tagline}</p>
                </div>
            </div>
        </footer>
    );
}
