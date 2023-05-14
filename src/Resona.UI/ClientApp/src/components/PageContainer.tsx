import { PropsWithChildren } from "react";
import "./PageContainer.scss"

const PageContainer = ({ children }: PropsWithChildren<{}>) => {
    return <div className="page-container">
        {children}
    </div>
}

export default PageContainer;